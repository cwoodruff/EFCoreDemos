SELECT "t"."Id", "t"."AlbumId", "t"."Bytes", "t"."Composer", "t"."GenreId", "t"."MediaTypeId", "t"."Milliseconds", "t"."Name", "t"."UnitPrice"
FROM "Track" AS "t"
WHERE ef_compare("t"."UnitPrice", '0.99') >= 0