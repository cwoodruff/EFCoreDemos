SELECT "t"."Id", "t"."Name", "a"."Title" AS "AlbumTitle"
FROM "Track" AS "t"
INNER JOIN "Album" AS "a" ON "t"."AlbumId" = "a"."Id"
WHERE ef_compare("t"."UnitPrice", '0.99') >= 0