extern alias transactions_and_savechanges;

using System.Transactions;
using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using transactions_and_savechanges::transactions_and_savechanges.Chinook;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// transactions-and-savechanges: several SaveChanges calls inside one explicit transaction are all-or-nothing,
    /// savepoints allow a partial rollback, and a TransactionScope only follows the code across threads/awaits
    /// when TransactionScopeAsyncFlowOption.Enabled is used.
    /// The demo's ChinookContext only has a parameterless constructor, so it is redirected to a Chinook copy.
    /// </summary>
    public class TransactionsAndSaveChangesTests : IDisposable
    {
        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext() => _db.Redirect(new ChinookContext());

        private static Invoice NewInvoice(int customerId) => new()
        {
            CustomerId = customerId,
            InvoiceDate = DateTime.UtcNow,
            BillingAddress = "1 Demo Way",
            BillingCity = "Demo",
            BillingCountry = "Demo",
            Total = 0m
        };

        private static InvoiceLine NewLine(int invoiceId, int trackId) => new()
        {
            InvoiceId = invoiceId,
            TrackId = trackId,
            UnitPrice = 0.99m,
            Quantity = 1
        };

        private (int Invoices, int Lines) CountRows()
        {
            using var db = CreateContext();
            return (db.Invoices.Count(), db.InvoiceLines.Count());
        }

        [Fact]
        public async Task ScenarioA_FailureMidway_RollbackDiscardsEverySaveChanges()
        {
            var before = CountRows();

            using (var db = CreateContext())
            {
                var customerId = db.Customers.OrderBy(c => c.Id).Select(c => c.Id).First();
                var trackId = db.Tracks.OrderBy(t => t.Id).Select(t => t.Id).First();

                await using var tx = await db.Database.BeginTransactionAsync();

                var invoice = NewInvoice(customerId);
                db.Invoices.Add(invoice);
                db.SaveChanges();
                db.InvoiceLines.Add(NewLine(invoice.Id, trackId));
                db.SaveChanges();
                db.InvoiceLines.Add(NewLine(invoice.Id, trackId));
                db.SaveChanges();

                // Inside the transaction the rows are visible...
                Assert.Equal(before.Invoices + 1, db.Invoices.Count());
                Assert.Equal(before.Lines + 2, db.InvoiceLines.Count());

                // ...then line #3 "fails" and the whole unit is rolled back.
                await tx.RollbackAsync();
            }

            Assert.Equal(before, CountRows());
        }

        [Fact]
        public async Task Commit_PersistsAllSaveChangesInTheTransaction()
        {
            var before = CountRows();

            using (var db = CreateContext())
            {
                var customerId = db.Customers.OrderBy(c => c.Id).Select(c => c.Id).First();
                var trackId = db.Tracks.OrderBy(t => t.Id).Select(t => t.Id).First();

                await using var tx = await db.Database.BeginTransactionAsync();
                var invoice = NewInvoice(customerId);
                db.Invoices.Add(invoice);
                db.SaveChanges();
                db.InvoiceLines.Add(NewLine(invoice.Id, trackId));
                db.SaveChanges();
                await tx.CommitAsync();
            }

            Assert.Equal((before.Invoices + 1, before.Lines + 1), CountRows());
        }

        [Fact]
        public async Task ScenarioB_RollbackToSavepoint_KeepsInvoice_DropsLines()
        {
            var before = CountRows();

            using (var db = CreateContext())
            {
                var customerId = db.Customers.OrderBy(c => c.Id).Select(c => c.Id).First();
                var trackId = db.Tracks.OrderBy(t => t.Id).Select(t => t.Id).First();

                await using var tx = await db.Database.BeginTransactionAsync();

                var invoice = NewInvoice(customerId);
                db.Invoices.Add(invoice);
                db.SaveChanges();
                await tx.CreateSavepointAsync("after-invoice");

                db.InvoiceLines.Add(NewLine(invoice.Id, trackId));
                db.SaveChanges();

                db.ChangeTracker.Clear();
                await tx.RollbackToSavepointAsync("after-invoice");

                Assert.Equal(before.Invoices + 1, db.Invoices.Count());
                Assert.Equal(before.Lines, db.InvoiceLines.Count());
                Assert.True(db.Invoices.Any(i => i.Id == invoice.Id));

                await tx.RollbackAsync();
            }

            Assert.Equal(before, CountRows());
        }

        [Fact]
        public void ScenarioC_DefaultTransactionScope_DoesNotFlow_AndMustBeDisposedOnItsOwnThread()
        {
            Transaction? onOwnerThread = null;
            Transaction? onOtherThread = null;
            Exception? disposeError = null;

            // Run on dedicated threads so the thread-local ambient transaction never leaks into other tests.
            var owner = new Thread(() =>
            {
                var scope = new TransactionScope();
                onOwnerThread = Transaction.Current;

                var other = new Thread(() =>
                {
                    onOtherThread = Transaction.Current;
                    try
                    {
                        scope.Complete();
                        scope.Dispose();
                    }
                    catch (Exception ex)
                    {
                        disposeError = ex;
                    }
                });
                other.Start();
                other.Join();
            });
            owner.Start();
            owner.Join();

            Assert.NotNull(onOwnerThread);
            Assert.Null(onOtherThread);
            Assert.IsType<InvalidOperationException>(disposeError);
        }

        [Fact]
        public async Task ScenarioC_AsyncFlowEnabled_SameTransactionAfterAwait()
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var before = Transaction.Current;

            // ConfigureAwait(false) on purpose: the continuation may resume on another thread, which is the point.
#pragma warning disable xUnit1030
            await Task.Delay(10).ConfigureAwait(false);
#pragma warning restore xUnit1030

            Assert.NotNull(before);
            Assert.NotNull(Transaction.Current);
            Assert.Equal(before.TransactionInformation.LocalIdentifier, Transaction.Current!.TransactionInformation.LocalIdentifier);
            scope.Complete();
        }
    }
}
