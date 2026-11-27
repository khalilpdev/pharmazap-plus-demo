-- Consolidação de clientes/atendimentos WhatsApp por últimos 8 dígitos.
-- Execute em homologação antes de produção e valide o resultado.
BEGIN;

-- 1) Mapeia clientes duplicados por FarmaciaId + últimos 8 dígitos do WhatsApp normalizado.
WITH clientes_base AS (
    SELECT
        c."Id",
        c."FarmaciaId",
        c."UltimoContato",
        c."DataCadastro",
        CASE
            WHEN length(raw_digits) >= 8 THEN right(raw_digits, 8)
            ELSE raw_digits
        END AS last8
    FROM (
        SELECT
            c."Id",
            c."FarmaciaId",
            c."UltimoContato",
            c."DataCadastro",
            CASE
                WHEN length(no_country) = 11 AND substr(no_country, 3, 1) = '9'
                    THEN substr(no_country, 1, 2) || substr(no_country, 4)
                WHEN length(no_country) = 9 AND substr(no_country, 1, 1) = '9'
                    THEN substr(no_country, 2)
                ELSE no_country
            END AS raw_digits
        FROM (
            SELECT
                c."Id",
                c."FarmaciaId",
                c."UltimoContato",
                c."DataCadastro",
                CASE
                    WHEN length(only_digits) >= 12 AND left(only_digits, 2) = '55'
                        THEN substr(only_digits, 3)
                    ELSE only_digits
                END AS no_country
            FROM (
                SELECT
                    c."Id",
                    c."FarmaciaId",
                    c."UltimoContato",
                    c."DataCadastro",
                    regexp_replace(coalesce(c."WhatsAppId", ''), '\D', '', 'g') AS only_digits
                FROM "Clientes" c
            ) c
        ) c
    ) c
),
grupos_duplicados AS (
    SELECT
        cb."FarmaciaId",
        cb.last8
    FROM clientes_base cb
    WHERE length(cb.last8) = 8
    GROUP BY cb."FarmaciaId", cb.last8
    HAVING count(*) > 1
),
ranking_clientes AS (
    SELECT
        cb."Id",
        cb."FarmaciaId",
        cb.last8,
        row_number() OVER (
            PARTITION BY cb."FarmaciaId", cb.last8
            ORDER BY
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM "Atendimentos" a
                        WHERE a."ClienteId" = cb."Id"
                          AND a."Status" = 'Pendente'
                    ) THEN 0
                    ELSE 1
                END,
                cb."UltimoContato" DESC NULLS LAST,
                cb."DataCadastro" DESC,
                cb."Id" ASC
        ) AS rn
    FROM clientes_base cb
    INNER JOIN grupos_duplicados gd
        ON gd."FarmaciaId" = cb."FarmaciaId"
       AND gd.last8 = cb.last8
),
mapa_clientes AS (
    SELECT
        r."Id" AS cliente_duplicado_id,
        p."Id" AS cliente_principal_id
    FROM ranking_clientes r
    INNER JOIN ranking_clientes p
        ON p."FarmaciaId" = r."FarmaciaId"
       AND p.last8 = r.last8
       AND p.rn = 1
    WHERE r.rn > 1
)
UPDATE "Atendimentos" a
SET "ClienteId" = mc.cliente_principal_id
FROM mapa_clientes mc
WHERE a."ClienteId" = mc.cliente_duplicado_id;

WITH clientes_base AS (
    SELECT
        c."Id",
        c."FarmaciaId",
        c."UltimoContato",
        c."DataCadastro",
        CASE
            WHEN length(raw_digits) >= 8 THEN right(raw_digits, 8)
            ELSE raw_digits
        END AS last8
    FROM (
        SELECT
            c."Id",
            c."FarmaciaId",
            c."UltimoContato",
            c."DataCadastro",
            CASE
                WHEN length(no_country) = 11 AND substr(no_country, 3, 1) = '9'
                    THEN substr(no_country, 1, 2) || substr(no_country, 4)
                WHEN length(no_country) = 9 AND substr(no_country, 1, 1) = '9'
                    THEN substr(no_country, 2)
                ELSE no_country
            END AS raw_digits
        FROM (
            SELECT
                c."Id",
                c."FarmaciaId",
                c."UltimoContato",
                c."DataCadastro",
                CASE
                    WHEN length(only_digits) >= 12 AND left(only_digits, 2) = '55'
                        THEN substr(only_digits, 3)
                    ELSE only_digits
                END AS no_country
            FROM (
                SELECT
                    c."Id",
                    c."FarmaciaId",
                    c."UltimoContato",
                    c."DataCadastro",
                    regexp_replace(coalesce(c."WhatsAppId", ''), '\D', '', 'g') AS only_digits
                FROM "Clientes" c
            ) c
        ) c
    ) c
),
grupos_duplicados AS (
    SELECT
        cb."FarmaciaId",
        cb.last8
    FROM clientes_base cb
    WHERE length(cb.last8) = 8
    GROUP BY cb."FarmaciaId", cb.last8
    HAVING count(*) > 1
),
ranking_clientes AS (
    SELECT
        cb."Id",
        cb."FarmaciaId",
        cb.last8,
        row_number() OVER (
            PARTITION BY cb."FarmaciaId", cb.last8
            ORDER BY
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM "Atendimentos" a
                        WHERE a."ClienteId" = cb."Id"
                          AND a."Status" = 'Pendente'
                    ) THEN 0
                    ELSE 1
                END,
                cb."UltimoContato" DESC NULLS LAST,
                cb."DataCadastro" DESC,
                cb."Id" ASC
        ) AS rn
    FROM clientes_base cb
    INNER JOIN grupos_duplicados gd
        ON gd."FarmaciaId" = cb."FarmaciaId"
       AND gd.last8 = cb.last8
),
mapa_clientes AS (
    SELECT
        r."Id" AS cliente_duplicado_id,
        p."Id" AS cliente_principal_id
    FROM ranking_clientes r
    INNER JOIN ranking_clientes p
        ON p."FarmaciaId" = r."FarmaciaId"
       AND p.last8 = r.last8
       AND p.rn = 1
    WHERE r.rn > 1
)
UPDATE "AgendamentosRecorrentes" ar
SET "ClienteId" = mc.cliente_principal_id
FROM mapa_clientes mc
WHERE ar."ClienteId" = mc.cliente_duplicado_id;

WITH clientes_base AS (
    SELECT
        c."Id",
        c."FarmaciaId",
        c."UltimoContato",
        c."DataCadastro",
        CASE
            WHEN length(raw_digits) >= 8 THEN right(raw_digits, 8)
            ELSE raw_digits
        END AS last8
    FROM (
        SELECT
            c."Id",
            c."FarmaciaId",
            c."UltimoContato",
            c."DataCadastro",
            CASE
                WHEN length(no_country) = 11 AND substr(no_country, 3, 1) = '9'
                    THEN substr(no_country, 1, 2) || substr(no_country, 4)
                WHEN length(no_country) = 9 AND substr(no_country, 1, 1) = '9'
                    THEN substr(no_country, 2)
                ELSE no_country
            END AS raw_digits
        FROM (
            SELECT
                c."Id",
                c."FarmaciaId",
                c."UltimoContato",
                c."DataCadastro",
                CASE
                    WHEN length(only_digits) >= 12 AND left(only_digits, 2) = '55'
                        THEN substr(only_digits, 3)
                    ELSE only_digits
                END AS no_country
            FROM (
                SELECT
                    c."Id",
                    c."FarmaciaId",
                    c."UltimoContato",
                    c."DataCadastro",
                    regexp_replace(coalesce(c."WhatsAppId", ''), '\D', '', 'g') AS only_digits
                FROM "Clientes" c
            ) c
        ) c
    ) c
),
grupos_duplicados AS (
    SELECT
        cb."FarmaciaId",
        cb.last8
    FROM clientes_base cb
    WHERE length(cb.last8) = 8
    GROUP BY cb."FarmaciaId", cb.last8
    HAVING count(*) > 1
),
ranking_clientes AS (
    SELECT
        cb."Id",
        cb."FarmaciaId",
        cb.last8,
        row_number() OVER (
            PARTITION BY cb."FarmaciaId", cb.last8
            ORDER BY
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM "Atendimentos" a
                        WHERE a."ClienteId" = cb."Id"
                          AND a."Status" = 'Pendente'
                    ) THEN 0
                    ELSE 1
                END,
                cb."UltimoContato" DESC NULLS LAST,
                cb."DataCadastro" DESC,
                cb."Id" ASC
        ) AS rn
    FROM clientes_base cb
    INNER JOIN grupos_duplicados gd
        ON gd."FarmaciaId" = cb."FarmaciaId"
       AND gd.last8 = cb.last8
),
mapa_clientes AS (
    SELECT
        r."Id" AS cliente_duplicado_id,
        p."Id" AS cliente_principal_id
    FROM ranking_clientes r
    INNER JOIN ranking_clientes p
        ON p."FarmaciaId" = r."FarmaciaId"
       AND p.last8 = r.last8
       AND p.rn = 1
    WHERE r.rn > 1
)
UPDATE "Pedidos" p
SET "ClienteId" = mc.cliente_principal_id
FROM mapa_clientes mc
WHERE p."ClienteId" = mc.cliente_duplicado_id;

WITH clientes_base AS (
    SELECT
        c."Id",
        c."FarmaciaId",
        c."UltimoContato",
        c."DataCadastro",
        CASE
            WHEN length(raw_digits) >= 8 THEN right(raw_digits, 8)
            ELSE raw_digits
        END AS last8
    FROM (
        SELECT
            c."Id",
            c."FarmaciaId",
            c."UltimoContato",
            c."DataCadastro",
            CASE
                WHEN length(no_country) = 11 AND substr(no_country, 3, 1) = '9'
                    THEN substr(no_country, 1, 2) || substr(no_country, 4)
                WHEN length(no_country) = 9 AND substr(no_country, 1, 1) = '9'
                    THEN substr(no_country, 2)
                ELSE no_country
            END AS raw_digits
        FROM (
            SELECT
                c."Id",
                c."FarmaciaId",
                c."UltimoContato",
                c."DataCadastro",
                CASE
                    WHEN length(only_digits) >= 12 AND left(only_digits, 2) = '55'
                        THEN substr(only_digits, 3)
                    ELSE only_digits
                END AS no_country
            FROM (
                SELECT
                    c."Id",
                    c."FarmaciaId",
                    c."UltimoContato",
                    c."DataCadastro",
                    regexp_replace(coalesce(c."WhatsAppId", ''), '\D', '', 'g') AS only_digits
                FROM "Clientes" c
            ) c
        ) c
    ) c
),
grupos_duplicados AS (
    SELECT
        cb."FarmaciaId",
        cb.last8
    FROM clientes_base cb
    WHERE length(cb.last8) = 8
    GROUP BY cb."FarmaciaId", cb.last8
    HAVING count(*) > 1
),
ranking_clientes AS (
    SELECT
        cb."Id",
        cb."FarmaciaId",
        cb.last8,
        row_number() OVER (
            PARTITION BY cb."FarmaciaId", cb.last8
            ORDER BY
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM "Atendimentos" a
                        WHERE a."ClienteId" = cb."Id"
                          AND a."Status" = 'Pendente'
                    ) THEN 0
                    ELSE 1
                END,
                cb."UltimoContato" DESC NULLS LAST,
                cb."DataCadastro" DESC,
                cb."Id" ASC
        ) AS rn
    FROM clientes_base cb
    INNER JOIN grupos_duplicados gd
        ON gd."FarmaciaId" = cb."FarmaciaId"
       AND gd.last8 = cb.last8
),
mapa_clientes AS (
    SELECT
        r."Id" AS cliente_duplicado_id,
        p."Id" AS cliente_principal_id
    FROM ranking_clientes r
    INNER JOIN ranking_clientes p
        ON p."FarmaciaId" = r."FarmaciaId"
       AND p.last8 = r.last8
       AND p.rn = 1
    WHERE r.rn > 1
)
UPDATE "ClienteObservacoes" co
SET "ClienteId" = mc.cliente_principal_id
FROM mapa_clientes mc
WHERE co."ClienteId" = mc.cliente_duplicado_id;

WITH clientes_base AS (
    SELECT
        c."Id",
        c."FarmaciaId",
        c."UltimoContato",
        c."DataCadastro",
        CASE
            WHEN length(raw_digits) >= 8 THEN right(raw_digits, 8)
            ELSE raw_digits
        END AS last8
    FROM (
        SELECT
            c."Id",
            c."FarmaciaId",
            c."UltimoContato",
            c."DataCadastro",
            CASE
                WHEN length(no_country) = 11 AND substr(no_country, 3, 1) = '9'
                    THEN substr(no_country, 1, 2) || substr(no_country, 4)
                WHEN length(no_country) = 9 AND substr(no_country, 1, 1) = '9'
                    THEN substr(no_country, 2)
                ELSE no_country
            END AS raw_digits
        FROM (
            SELECT
                c."Id",
                c."FarmaciaId",
                c."UltimoContato",
                c."DataCadastro",
                CASE
                    WHEN length(only_digits) >= 12 AND left(only_digits, 2) = '55'
                        THEN substr(only_digits, 3)
                    ELSE only_digits
                END AS no_country
            FROM (
                SELECT
                    c."Id",
                    c."FarmaciaId",
                    c."UltimoContato",
                    c."DataCadastro",
                    regexp_replace(coalesce(c."WhatsAppId", ''), '\D', '', 'g') AS only_digits
                FROM "Clientes" c
            ) c
        ) c
    ) c
),
grupos_duplicados AS (
    SELECT
        cb."FarmaciaId",
        cb.last8
    FROM clientes_base cb
    WHERE length(cb.last8) = 8
    GROUP BY cb."FarmaciaId", cb.last8
    HAVING count(*) > 1
),
ranking_clientes AS (
    SELECT
        cb."Id",
        cb."FarmaciaId",
        cb.last8,
        row_number() OVER (
            PARTITION BY cb."FarmaciaId", cb.last8
            ORDER BY
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM "Atendimentos" a
                        WHERE a."ClienteId" = cb."Id"
                          AND a."Status" = 'Pendente'
                    ) THEN 0
                    ELSE 1
                END,
                cb."UltimoContato" DESC NULLS LAST,
                cb."DataCadastro" DESC,
                cb."Id" ASC
        ) AS rn
    FROM clientes_base cb
    INNER JOIN grupos_duplicados gd
        ON gd."FarmaciaId" = cb."FarmaciaId"
       AND gd.last8 = cb.last8
),
mapa_clientes AS (
    SELECT
        r."Id" AS cliente_duplicado_id,
        p."Id" AS cliente_principal_id
    FROM ranking_clientes r
    INNER JOIN ranking_clientes p
        ON p."FarmaciaId" = r."FarmaciaId"
       AND p.last8 = r.last8
       AND p.rn = 1
    WHERE r.rn > 1
)
UPDATE "ClienteTags" ct
SET "ClienteId" = mc.cliente_principal_id
FROM mapa_clientes mc
WHERE ct."ClienteId" = mc.cliente_duplicado_id;

-- Remove tags duplicadas criadas após a consolidação.
DELETE FROM "ClienteTags" ct
USING "ClienteTags" ct_keep
WHERE ct."Id" > ct_keep."Id"
  AND ct."ClienteId" = ct_keep."ClienteId"
  AND ct."Tag" = ct_keep."Tag";

-- 2) Consolida atendimentos pendentes WhatsApp por FarmaciaId + últimos 8 dígitos.
WITH atendimentos_base AS (
    SELECT
        a."Id",
        a."FarmaciaId",
        a."ClienteId",
        a."WhatsAppId",
        a."DataInicio",
        CASE
            WHEN length(raw_digits) >= 8 THEN right(raw_digits, 8)
            ELSE raw_digits
        END AS last8
    FROM (
        SELECT
            a."Id",
            a."FarmaciaId",
            a."ClienteId",
            a."WhatsAppId",
            a."DataInicio",
            CASE
                WHEN length(no_country) = 11 AND substr(no_country, 3, 1) = '9'
                    THEN substr(no_country, 1, 2) || substr(no_country, 4)
                WHEN length(no_country) = 9 AND substr(no_country, 1, 1) = '9'
                    THEN substr(no_country, 2)
                ELSE no_country
            END AS raw_digits
        FROM (
            SELECT
                a."Id",
                a."FarmaciaId",
                a."ClienteId",
                a."WhatsAppId",
                a."DataInicio",
                CASE
                    WHEN length(only_digits) >= 12 AND left(only_digits, 2) = '55'
                        THEN substr(only_digits, 3)
                    ELSE only_digits
                END AS no_country
            FROM (
                SELECT
                    a."Id",
                    a."FarmaciaId",
                    a."ClienteId",
                    a."WhatsAppId",
                    a."DataInicio",
                    regexp_replace(coalesce(a."WhatsAppId", ''), '\D', '', 'g') AS only_digits
                FROM "Atendimentos" a
                WHERE a."Status" = 'Pendente'
                  AND a."Canal" = 'WhatsApp'
            ) a
        ) a
    ) a
),
grupos_atendimentos AS (
    SELECT
        ab."FarmaciaId",
        ab.last8
    FROM atendimentos_base ab
    WHERE length(ab.last8) = 8
    GROUP BY ab."FarmaciaId", ab.last8
    HAVING count(*) > 1
),
ranking_atendimentos AS (
    SELECT
        ab.*,
        row_number() OVER (
            PARTITION BY ab."FarmaciaId", ab.last8
            ORDER BY ab."DataInicio" ASC, ab."Id" ASC
        ) AS rn
    FROM atendimentos_base ab
    INNER JOIN grupos_atendimentos ga
        ON ga."FarmaciaId" = ab."FarmaciaId"
       AND ga.last8 = ab.last8
),
mapa_atendimentos AS (
    SELECT
        r."Id" AS atendimento_duplicado_id,
        p."Id" AS atendimento_principal_id
    FROM ranking_atendimentos r
    INNER JOIN ranking_atendimentos p
        ON p."FarmaciaId" = r."FarmaciaId"
       AND p.last8 = r.last8
       AND p.rn = 1
    WHERE r.rn > 1
)
UPDATE "Atendimentos" a_keep
SET "ClienteId" = COALESCE(a_keep."ClienteId", a_src."ClienteId"),
    "WhatsAppId" = COALESCE(NULLIF(a_keep."WhatsAppId", ''), a_src."WhatsAppId")
FROM mapa_atendimentos ma
INNER JOIN "Atendimentos" a_src ON a_src."Id" = ma.atendimento_duplicado_id
WHERE a_keep."Id" = ma.atendimento_principal_id;

WITH atendimentos_base AS (
    SELECT
        a."Id",
        a."FarmaciaId",
        a."ClienteId",
        a."WhatsAppId",
        a."DataInicio",
        CASE
            WHEN length(raw_digits) >= 8 THEN right(raw_digits, 8)
            ELSE raw_digits
        END AS last8
    FROM (
        SELECT
            a."Id",
            a."FarmaciaId",
            a."ClienteId",
            a."WhatsAppId",
            a."DataInicio",
            CASE
                WHEN length(no_country) = 11 AND substr(no_country, 3, 1) = '9'
                    THEN substr(no_country, 1, 2) || substr(no_country, 4)
                WHEN length(no_country) = 9 AND substr(no_country, 1, 1) = '9'
                    THEN substr(no_country, 2)
                ELSE no_country
            END AS raw_digits
        FROM (
            SELECT
                a."Id",
                a."FarmaciaId",
                a."ClienteId",
                a."WhatsAppId",
                a."DataInicio",
                CASE
                    WHEN length(only_digits) >= 12 AND left(only_digits, 2) = '55'
                        THEN substr(only_digits, 3)
                    ELSE only_digits
                END AS no_country
            FROM (
                SELECT
                    a."Id",
                    a."FarmaciaId",
                    a."ClienteId",
                    a."WhatsAppId",
                    a."DataInicio",
                    regexp_replace(coalesce(a."WhatsAppId", ''), '\D', '', 'g') AS only_digits
                FROM "Atendimentos" a
                WHERE a."Status" = 'Pendente'
                  AND a."Canal" = 'WhatsApp'
            ) a
        ) a
    ) a
),
grupos_atendimentos AS (
    SELECT
        ab."FarmaciaId",
        ab.last8
    FROM atendimentos_base ab
    WHERE length(ab.last8) = 8
    GROUP BY ab."FarmaciaId", ab.last8
    HAVING count(*) > 1
),
ranking_atendimentos AS (
    SELECT
        ab.*,
        row_number() OVER (
            PARTITION BY ab."FarmaciaId", ab.last8
            ORDER BY ab."DataInicio" ASC, ab."Id" ASC
        ) AS rn
    FROM atendimentos_base ab
    INNER JOIN grupos_atendimentos ga
        ON ga."FarmaciaId" = ab."FarmaciaId"
       AND ga.last8 = ab.last8
),
mapa_atendimentos AS (
    SELECT
        r."Id" AS atendimento_duplicado_id,
        p."Id" AS atendimento_principal_id
    FROM ranking_atendimentos r
    INNER JOIN ranking_atendimentos p
        ON p."FarmaciaId" = r."FarmaciaId"
       AND p.last8 = r.last8
       AND p.rn = 1
    WHERE r.rn > 1
)
UPDATE "Mensagens" m
SET "AtendimentoId" = ma.atendimento_principal_id
FROM mapa_atendimentos ma
WHERE m."AtendimentoId" = ma.atendimento_duplicado_id;

WITH atendimentos_base AS (
    SELECT
        a."Id",
        a."FarmaciaId",
        a."ClienteId",
        a."WhatsAppId",
        a."DataInicio",
        CASE
            WHEN length(raw_digits) >= 8 THEN right(raw_digits, 8)
            ELSE raw_digits
        END AS last8
    FROM (
        SELECT
            a."Id",
            a."FarmaciaId",
            a."ClienteId",
            a."WhatsAppId",
            a."DataInicio",
            CASE
                WHEN length(no_country) = 11 AND substr(no_country, 3, 1) = '9'
                    THEN substr(no_country, 1, 2) || substr(no_country, 4)
                WHEN length(no_country) = 9 AND substr(no_country, 1, 1) = '9'
                    THEN substr(no_country, 2)
                ELSE no_country
            END AS raw_digits
        FROM (
            SELECT
                a."Id",
                a."FarmaciaId",
                a."ClienteId",
                a."WhatsAppId",
                a."DataInicio",
                CASE
                    WHEN length(only_digits) >= 12 AND left(only_digits, 2) = '55'
                        THEN substr(only_digits, 3)
                    ELSE only_digits
                END AS no_country
            FROM (
                SELECT
                    a."Id",
                    a."FarmaciaId",
                    a."ClienteId",
                    a."WhatsAppId",
                    a."DataInicio",
                    regexp_replace(coalesce(a."WhatsAppId", ''), '\D', '', 'g') AS only_digits
                FROM "Atendimentos" a
                WHERE a."Status" = 'Pendente'
                  AND a."Canal" = 'WhatsApp'
            ) a
        ) a
    ) a
),
grupos_atendimentos AS (
    SELECT
        ab."FarmaciaId",
        ab.last8
    FROM atendimentos_base ab
    WHERE length(ab.last8) = 8
    GROUP BY ab."FarmaciaId", ab.last8
    HAVING count(*) > 1
),
ranking_atendimentos AS (
    SELECT
        ab.*,
        row_number() OVER (
            PARTITION BY ab."FarmaciaId", ab.last8
            ORDER BY ab."DataInicio" ASC, ab."Id" ASC
        ) AS rn
    FROM atendimentos_base ab
    INNER JOIN grupos_atendimentos ga
        ON ga."FarmaciaId" = ab."FarmaciaId"
       AND ga.last8 = ab.last8
),
mapa_atendimentos AS (
    SELECT
        r."Id" AS atendimento_duplicado_id,
        p."Id" AS atendimento_principal_id
    FROM ranking_atendimentos r
    INNER JOIN ranking_atendimentos p
        ON p."FarmaciaId" = r."FarmaciaId"
       AND p.last8 = r.last8
       AND p.rn = 1
    WHERE r.rn > 1
)
UPDATE "AgendamentosRecorrentes" ar
SET "AtendimentoId" = ma.atendimento_principal_id
FROM mapa_atendimentos ma
WHERE ar."AtendimentoId" = ma.atendimento_duplicado_id;

WITH atendimentos_base AS (
    SELECT
        a."Id",
        a."FarmaciaId",
        a."ClienteId",
        a."WhatsAppId",
        a."DataInicio",
        CASE
            WHEN length(raw_digits) >= 8 THEN right(raw_digits, 8)
            ELSE raw_digits
        END AS last8
    FROM (
        SELECT
            a."Id",
            a."FarmaciaId",
            a."ClienteId",
            a."WhatsAppId",
            a."DataInicio",
            CASE
                WHEN length(no_country) = 11 AND substr(no_country, 3, 1) = '9'
                    THEN substr(no_country, 1, 2) || substr(no_country, 4)
                WHEN length(no_country) = 9 AND substr(no_country, 1, 1) = '9'
                    THEN substr(no_country, 2)
                ELSE no_country
            END AS raw_digits
        FROM (
            SELECT
                a."Id",
                a."FarmaciaId",
                a."ClienteId",
                a."WhatsAppId",
                a."DataInicio",
                CASE
                    WHEN length(only_digits) >= 12 AND left(only_digits, 2) = '55'
                        THEN substr(only_digits, 3)
                    ELSE only_digits
                END AS no_country
            FROM (
                SELECT
                    a."Id",
                    a."FarmaciaId",
                    a."ClienteId",
                    a."WhatsAppId",
                    a."DataInicio",
                    regexp_replace(coalesce(a."WhatsAppId", ''), '\D', '', 'g') AS only_digits
                FROM "Atendimentos" a
                WHERE a."Status" = 'Pendente'
                  AND a."Canal" = 'WhatsApp'
            ) a
        ) a
    ) a
),
grupos_atendimentos AS (
    SELECT
        ab."FarmaciaId",
        ab.last8
    FROM atendimentos_base ab
    WHERE length(ab.last8) = 8
    GROUP BY ab."FarmaciaId", ab.last8
    HAVING count(*) > 1
),
ranking_atendimentos AS (
    SELECT
        ab.*,
        row_number() OVER (
            PARTITION BY ab."FarmaciaId", ab.last8
            ORDER BY ab."DataInicio" ASC, ab."Id" ASC
        ) AS rn
    FROM atendimentos_base ab
    INNER JOIN grupos_atendimentos ga
        ON ga."FarmaciaId" = ab."FarmaciaId"
       AND ga.last8 = ab.last8
),
mapa_atendimentos AS (
    SELECT
        r."Id" AS atendimento_duplicado_id,
        p."Id" AS atendimento_principal_id
    FROM ranking_atendimentos r
    INNER JOIN ranking_atendimentos p
        ON p."FarmaciaId" = r."FarmaciaId"
       AND p.last8 = r.last8
       AND p.rn = 1
    WHERE r.rn > 1
)
DELETE FROM "Atendimentos" a
USING mapa_atendimentos ma
WHERE a."Id" = ma.atendimento_duplicado_id;

-- 3) Remove clientes duplicados após mover todos os vínculos.
WITH clientes_base AS (
    SELECT
        c."Id",
        c."FarmaciaId",
        c."UltimoContato",
        c."DataCadastro",
        CASE
            WHEN length(raw_digits) >= 8 THEN right(raw_digits, 8)
            ELSE raw_digits
        END AS last8
    FROM (
        SELECT
            c."Id",
            c."FarmaciaId",
            c."UltimoContato",
            c."DataCadastro",
            CASE
                WHEN length(no_country) = 11 AND substr(no_country, 3, 1) = '9'
                    THEN substr(no_country, 1, 2) || substr(no_country, 4)
                WHEN length(no_country) = 9 AND substr(no_country, 1, 1) = '9'
                    THEN substr(no_country, 2)
                ELSE no_country
            END AS raw_digits
        FROM (
            SELECT
                c."Id",
                c."FarmaciaId",
                c."UltimoContato",
                c."DataCadastro",
                CASE
                    WHEN length(only_digits) >= 12 AND left(only_digits, 2) = '55'
                        THEN substr(only_digits, 3)
                    ELSE only_digits
                END AS no_country
            FROM (
                SELECT
                    c."Id",
                    c."FarmaciaId",
                    c."UltimoContato",
                    c."DataCadastro",
                    regexp_replace(coalesce(c."WhatsAppId", ''), '\D', '', 'g') AS only_digits
                FROM "Clientes" c
            ) c
        ) c
    ) c
),
grupos_duplicados AS (
    SELECT
        cb."FarmaciaId",
        cb.last8
    FROM clientes_base cb
    WHERE length(cb.last8) = 8
    GROUP BY cb."FarmaciaId", cb.last8
    HAVING count(*) > 1
),
ranking_clientes AS (
    SELECT
        cb."Id",
        cb."FarmaciaId",
        cb.last8,
        row_number() OVER (
            PARTITION BY cb."FarmaciaId", cb.last8
            ORDER BY
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM "Atendimentos" a
                        WHERE a."ClienteId" = cb."Id"
                          AND a."Status" = 'Pendente'
                    ) THEN 0
                    ELSE 1
                END,
                cb."UltimoContato" DESC NULLS LAST,
                cb."DataCadastro" DESC,
                cb."Id" ASC
        ) AS rn
    FROM clientes_base cb
    INNER JOIN grupos_duplicados gd
        ON gd."FarmaciaId" = cb."FarmaciaId"
       AND gd.last8 = cb.last8
),
mapa_clientes AS (
    SELECT
        r."Id" AS cliente_duplicado_id
    FROM ranking_clientes r
    WHERE r.rn > 1
)
DELETE FROM "Clientes" c
USING mapa_clientes mc
WHERE c."Id" = mc.cliente_duplicado_id;

COMMIT;
