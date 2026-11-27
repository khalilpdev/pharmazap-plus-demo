-- Atualiza um canal WhatsApp não oficial já cadastrado e sua configuração efetiva.
--
-- Uso:
-- docker exec -i pharmazap-postgres psql -v ON_ERROR_STOP=1 -U postgres -d pharmazap \
--   -v farmacia_id=1 \
--   -v channel_id=1 \
--   -v instance_name='5562998808389' \
--   -v evolution_api_url='http://evolution-go:8080' \
--   -v evolution_api_key="$EVOLUTION_API_KEY" \
--   < scripts/update-evolution-channel.sql
--
-- Nunca informe a chave diretamente no arquivo ou em logs compartilhados.

\if :{?farmacia_id}
\else
\echo 'Informe -v farmacia_id=<id>.'
\quit
\endif

\if :{?channel_id}
\else
\echo 'Informe -v channel_id=<id>.'
\quit
\endif

\if :{?instance_name}
\else
\echo 'Informe -v instance_name=<instancia>.'
\quit
\endif

\if :{?evolution_api_url}
\else
\echo 'Informe -v evolution_api_url=<url-base>.'
\quit
\endif

\if :{?evolution_api_key}
\else
\echo 'Informe -v evolution_api_key=<chave>.'
\quit
\endif

\set ON_ERROR_STOP on

BEGIN;

CREATE TEMP TABLE evolution_channel_update_parameters AS
SELECT
    :farmacia_id::integer AS farmacia_id,
    :channel_id::integer AS channel_id,
    :'instance_name'::text AS instance_name,
    regexp_replace(rtrim(:'evolution_api_url', '/'), '/webhook$', '', 'i') AS evolution_api_url,
    :'evolution_api_key'::text AS evolution_api_key;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM "FarmaciaIntegracoes" i
        JOIN evolution_channel_update_parameters p ON p.farmacia_id = i."FarmaciaId"
    ) THEN
        RAISE EXCEPTION 'Nenhuma integração encontrada para a farmácia informada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM "Canais" c
        JOIN "FarmaciaIntegracoes" i ON i."Id" = c."FarmaciaIntegracosId"
        JOIN evolution_channel_update_parameters p ON p.channel_id = c."Id" AND p.farmacia_id = i."FarmaciaId"
        WHERE c."Tipo" = 1
          AND c."IsOfficial" = false
    ) THEN
        RAISE EXCEPTION 'O canal informado não é um WhatsApp não oficial da farmácia informada.';
    END IF;
END $$;

UPDATE "FarmaciaIntegracoes" i
SET "ChaveWhatsApp" = p.instance_name,
    "WhatsAppApiUrl" = p.evolution_api_url,
    "WhatsAppApiKey" = p.evolution_api_key
FROM evolution_channel_update_parameters p
WHERE i."FarmaciaId" = p.farmacia_id;

UPDATE "Canais" c
SET "InstanceName" = p.instance_name,
    "ApiUrl" = p.evolution_api_url,
    "ApiKey" = p.evolution_api_key
FROM evolution_channel_update_parameters p
WHERE c."Id" = p.channel_id;

SELECT
    c."Id" AS "CanalId",
    c."Nome" AS "Canal",
    c."InstanceName",
    c."ApiUrl",
    c."IsOfficial",
    i."ChaveWhatsApp",
    i."WhatsAppApiUrl",
    CASE WHEN NULLIF(i."WhatsAppApiKey", '') IS NULL THEN 'AUSENTE' ELSE 'CONFIGURADA' END AS "ChaveConfigurada"
FROM "Canais" c
JOIN "FarmaciaIntegracoes" i ON i."Id" = c."FarmaciaIntegracosId"
JOIN evolution_channel_update_parameters p ON p.channel_id = c."Id" AND p.farmacia_id = i."FarmaciaId";

COMMIT;
