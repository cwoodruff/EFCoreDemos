To restore a SQL Server database from a BAK file, you can use the <a href="https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms?view=sql-server-ver16" target="_blank">SQL Server Management Studio (SSMS)</a> tool. Here are the general steps:

1. Open SSMS and connect to the SQL Server instance where you want to restore the database.
2. In Object Explorer, right-click on the "Databases" node and select "Restore Database."
3. In the "General" page of the "Restore Database" dialog box, select "From device" and then click on the "..." button to browse for the BAK file.
4. In the "Files" page, you can specify the file locations for the restored database and its transaction logs.
5. In the "Options" page, you can specify options such as recovery state, and if you want to overwrite an existing database with the same name.
6. Click "OK" to start the restore process.

Once the restore process is complete, the database will be available for use.

Note: Make sure you have the rights to perform the restore and have a backup of the current database if you want to keep the original data.

Note - Find the Wide World Importers (Standard) BAK file in this <a href="https://github.com/Microsoft/sql-server-samples/releases/download/wide-world-importers-v1.0/WideWorldImporters-Standard.bak" target="_blank">GitHub repo</a>.