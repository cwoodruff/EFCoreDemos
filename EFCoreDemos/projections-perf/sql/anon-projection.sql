SELECT "t"."Id", "t"."Name", "t"."AlbumId"
FROM "Track" AS "t"
WHERE ef_compare("t"."UnitPrice", '0.99') >= 0