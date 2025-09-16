-- Métricas padronizadas por tabela
CREATE TABLE IF NOT EXISTS dq_table_metrics (
    id BIGSERIAL PRIMARY KEY,
    schema_name TEXT NOT NULL,
    table_name TEXT NOT NULL,
    metric_group TEXT NOT NULL,  -- 'volume', 'nulls', 'temporal', 'integrity'
    metric_name TEXT NOT NULL,   -- 'row_count', 'table_size_mb', 'null_rate'
    metric_value NUMERIC,
    collected_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT unique_table_metric UNIQUE (schema_name, table_name, metric_group, metric_name, collected_at)
);

-- Métricas padronizadas por coluna
CREATE TABLE IF NOT EXISTS dq_column_metrics (
    id BIGSERIAL PRIMARY KEY,
    schema_name TEXT NOT NULL,
    table_name TEXT NOT NULL,
    column_name TEXT NOT NULL,
    metric_name TEXT NOT NULL,   -- 'null_rate', 'distinct_count', 'avg_len', 'std_len'
    metric_value NUMERIC,
    collected_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT unique_column_metric UNIQUE (schema_name, table_name, column_name, metric_name, collected_at)
);

-- Pré-voo e testes de sanidade da LLM
CREATE TABLE IF NOT EXISTS dq_preflight_results (
    id BIGSERIAL PRIMARY KEY,
    schema_name TEXT NOT NULL,
    table_name TEXT,  -- NULL para testes gerais
    test_type TEXT NOT NULL,     -- 'connectivity', 'schema_introspection', 'sanity'
    test_name TEXT NOT NULL,
    sql_executed TEXT NOT NULL,
    expectation TEXT,
    success BOOLEAN NOT NULL,
    result_data JSONB,  -- resultado da query
    error_message TEXT,
    executed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Candidatos de regras gerados pela LLM (não executados ainda)
CREATE TABLE IF NOT EXISTS dq_rule_candidates (
    id BIGSERIAL PRIMARY KEY,
    schema_name TEXT NOT NULL,
    table_name TEXT NOT NULL,
    column_name TEXT,
    dimension TEXT NOT NULL,     -- 'completude', 'consistencia', 'conformidade', 'precisao'
    rule_name TEXT NOT NULL,
    check_sql TEXT NOT NULL,
    description TEXT,
    severity TEXT DEFAULT 'medium',  -- 'low', 'medium', 'high', 'critical'
    auto_generated BOOLEAN DEFAULT true,
    approved_by_user BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT unique_rule_candidate UNIQUE (schema_name, table_name, column_name, rule_name)
);

-- Resultados de execução das regras
CREATE TABLE IF NOT EXISTS dq_rule_executions (
    id BIGSERIAL PRIMARY KEY,
    rule_candidate_id BIGINT REFERENCES dq_rule_candidates(id),
    total_records INTEGER NOT NULL,
    valid_records INTEGER NOT NULL,
    invalid_records INTEGER NOT NULL,
    success BOOLEAN NOT NULL,
    error_message TEXT,
    execution_time_ms INTEGER,
    executed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Índices para performance
CREATE INDEX IF NOT EXISTS idx_table_metrics_schema_table ON dq_table_metrics(schema_name, table_name);
CREATE INDEX IF NOT EXISTS idx_column_metrics_schema_table_column ON dq_column_metrics(schema_name, table_name, column_name);
CREATE INDEX IF NOT EXISTS idx_preflight_schema_table ON dq_preflight_results(schema_name, table_name);
CREATE INDEX IF NOT EXISTS idx_rule_candidates_schema_table ON dq_rule_candidates(schema_name, table_name);
CREATE INDEX IF NOT EXISTS idx_rule_executions_rule_id ON dq_rule_executions(rule_candidate_id);