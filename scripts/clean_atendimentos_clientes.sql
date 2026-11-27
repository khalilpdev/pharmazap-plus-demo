-- ============================================================
-- Script: clean_atendimentos_clientes.sql
-- Finalidade: Limpar TODOS os atendimentos e clientes do banco
--             para recomeçar do zero.
-- Ordem de deleção respeita as FK constraints:
--   PedidoItens → Pedidos → Mensagens → ClienteTags →
--   ClienteObservacoes → AtividadesRecentes →
--   AgendamentosRecorrentes → Atendimentos → Clientes
-- ============================================================

BEGIN;

-- 1. Itens de pedido (fk: PedidoId)
DELETE FROM "PedidoItens";

-- 2. Pedidos (fk: ClienteId, AtendimentoId)
DELETE FROM "Pedidos";

-- 3. Mensagens (fk: AtendimentoId)
DELETE FROM "Mensagens";

-- 4. Tags de cliente (fk: ClienteId)
DELETE FROM "ClienteTags";

-- 5. Observações de cliente (fk: ClienteId)
DELETE FROM "ClienteObservacoes";

-- 6. Atividades recentes (fk: AtendimentoId)
DELETE FROM "AtividadesRecentes";

-- 7. Agendamentos recorrentes (fk: ClienteId, AtendimentoId)
DELETE FROM "AgendamentosRecorrentes";

-- 8. Atendimentos (fk: ClienteId)
DELETE FROM "Atendimentos";

-- 9. Clientes
DELETE FROM "Clientes";

COMMIT;

-- ============================================================
-- Para executar (PostgreSQL local no host, ver .env: POSTGRES_DB/POSTGRES_USER):
--
--   psql -U pharmazap -d pharmazaptest -h localhost \
--     < scripts/clean_atendimentos_clientes.sql
--
-- Se o PostgreSQL estiver rodando em container Docker (container
-- "pharmazap-postgres"), use:
--
--   docker exec -i pharmazap-postgres \
--     psql -U pharmazap -d pharmazaptest \
--     < scripts/clean_atendimentos_clientes.sql
-- ============================================================
