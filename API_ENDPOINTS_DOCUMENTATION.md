# 📚 API Endpoints - DbConnect Data Profiling System

## 📊 Visão Geral

O sistema DbConnect oferece uma arquitetura completa de data profiling e quality com dois níveis de API:

- **🔧 APIs Tradicionais**: Sistema original robusto e estável
- **🚀 APIs Enhanced**: Sistema revolutionário com IA e descoberta automática

---

## 🔧 APIs TRADICIONAIS (Sistema Original)

### 👤 Authentication & User Management

#### `/api/auth` - Autenticação
- **POST** `/api/auth/register` - Registrar novo usuário
- **POST** `/api/auth/login` - Login de usuário
- **POST** `/api/auth/logout` - Logout
- **GET** `/api/auth/me` - Informações do usuário logado

### 🔌 Connection Profiles

#### `/api/profiles` - Gerenciamento de Perfis de Conexão
- **GET** `/api/profiles` - Listar todos os perfis do usuário
- **POST** `/api/profiles` - Criar novo perfil de conexão
- **PUT** `/api/profiles/{id}` - Atualizar perfil existente
- **DELETE** `/api/profiles/{id}` - Deletar perfil
- **POST** `/api/profiles/{id}/test` - Testar conexão do perfil
- **POST** `/api/profiles/{id}/activate` - Ativar perfil para uso

**Exemplo de Payload**:
```json
{
  "name": "PostgreSQL Produção",
  "kind": "PostgreSql",
  "hostOrFile": "localhost",
  "port": 5432,
  "database": "meu_banco",
  "username": "usuario",
  "password": "senha"
}
```

### 🗃️ Database Information

#### `/api/database-info` - Informações do Banco
- **GET** `/api/database-info` - Obter informações gerais do banco conectado
  - Retorna: nome, tamanho, versão, estatísticas, top tabelas, schemas

### 📋 Tables & Schema Exploration

#### `/api/tables` - Exploração de Tabelas
- **GET** `/api/tables` - Listar todas as tabelas do banco
- **GET** `/api/tables/{schema}/{tableName}` - Detalhes de uma tabela específica
- **GET** `/api/tables/{schema}/{tableName}/columns` - Colunas de uma tabela
- **GET** `/api/tables/{schema}/{tableName}/sample` - Amostra de dados (primeiras linhas)

### 📊 Essential Metrics (Métricas Básicas)

#### `/api/essential-metrics` - Métricas Simples
- **POST** `/api/essential-metrics/collect` - Coletar métricas básicas de uma tabela
  - Input: `{ "schema": "public", "tableName": "usuarios" }`
  - Output: Contagens, nulos, distintos, tipos de dados

- **POST** `/api/essential-metrics/collect-advanced` - Métricas avançadas
  - Input: `{ "schema": "public", "tableName": "usuarios" }`
  - Output: Padrões (CPF, Email, etc.), outliers, correlações, relações status-data

**Capacidades das Métricas Avançadas**:
- ✅ Validação automática de CPF, CNPJ, Email, CEP, Telefone
- ✅ Detecção de outliers estatísticos (regra 3σ) com paginação
- ✅ Análise de correlações numéricas
- ✅ Detecção de relações Status ↔ Data inconsistentes

### 🎯 Data Quality V2 (Sistema de Qualidade Atual)

#### `/api/data-quality` - Sistema de Qualidade de Dados
- **POST** `/api/data-quality/analyze` - Análise completa de qualidade
  - **FASE 1 - Preflight**: Coleta schema + amostra + contexto
  - **FASE 2 - AI Rules**: IA gera regras SQL contextuais
  - **FASE 3 - Execution**: Executa validações em paralelo
  - **FASE 4 - Dashboard**: Organiza resultados + visualizações

**Pipeline Detalhado**:
```json
{
  "tableName": "s_documentos",
  "schemaName": "public",
  "apiKey": "sk-...",
  "options": {
    "enableAI": true,
    "maxRules": 10,
    "includeVisualization": true
  }
}
```

**Retorna**:
- ✅ 10 regras SQL geradas por IA
- ✅ Resultados de execução detalhados
- ✅ Gráficos automáticos (Recharts)
- ✅ Métricas de performance
- ✅ Recomendações de ação

#### `/api/data-quality/outliers` - Outliers Paginados
- **GET** `/api/data-quality/outliers?tableName={table}&schemaName={schema}&columnName={column}&page={page}&pageSize={size}`
  - Outliers ordenados por magnitude
  - Paginação completa com contexto
  - Dados completos da linha para análise

### 🔍 Schema Discovery (Discovery Atual)

#### `/api/schema-discovery` - Descoberta de Schema
- **POST** `/api/schema-discovery/discover` - Descoberta automática PostgreSQL
  - Baseado em `information_schema`
  - Detecta relacionamentos FK declarados
  - Identifica relacionamentos implícitos por nomenclatura
  - Ranking de importância para validações cruzadas

**Capacidades**:
- ✅ Todas as tabelas + metadados
- ✅ Foreign keys declaradas
- ✅ Relacionamentos implícitos (ID_*, *_ID)
- ✅ Hierarquia de importância (1-10)
- ✅ Oportunidades de validação

---

## 🚀 APIs ENHANCED (Sistema Revolucionário com IA)

### 🎯 Enhanced Data Quality (NOVO!)

#### `/api/enhanced-data-quality` - Sistema IA Enhanced

#### **POST** `/api/enhanced-data-quality/analyze-complete` ⭐ **PRINCIPAL**
**Pipeline Completo: Discovery → Context → AI → Translation → Execution → Visualization**

**Input**:
```json
{
  "tableName": "s_documentos",
  "businessContext": "Sistema de documentos corporativos com workflow de aprovação",
  "apiKey": "sk-...",
  "includeSQL": false
}
```

**Output Completo**:
```json
{
  "success": true,
  "analysis": {
    "focusTable": "s_documentos",
    "executionTime": 23.4,
    "validationsExecuted": 15,
    "issuesDetected": 1247,
    "averageQuality": 87.3,
    "performanceRating": "GOOD"
  },
  "summary": {
    "totalValidations": 15,
    "successfulExecutions": 14,
    "failedExecutions": 1,
    "totalIssues": 1247,
    "highPriorityIssues": 3,
    "mediumPriorityIssues": 8,
    "recommendations": [
      "🚨 CRÍTICO: 3 problemas de alta prioridade requerem atenção imediata",
      "🔗 2 problemas de integridade referencial - verificar FKs e relacionamentos"
    ]
  },
  "validations": [
    {
      "id": "uuid",
      "description": "Verificar se documentos ATIVO têm clientes ATIVO relacionados",
      "priority": 9,
      "type": "STATUS_CONSISTENCY",
      "status": "ISSUES_FOUND",
      "issuesDetected": 342,
      "totalRecords": 11377318,
      "qualityPercentage": 97.0,
      "executionDuration": 1234.5
    }
  ],
  "dashboard": {
    "id": "uuid",
    "title": "Data Quality Dashboard - s_documentos",
    "layout": { "rows": [...] },
    "visualizations": [
      {
        "id": "uuid",
        "title": "Overview de Qualidade",
        "chartType": "DashboardOverview",
        "data": { ... },
        "configuration": { ... }
      }
    ],
    "insights": [
      "✅ 12 validações passaram sem problemas",
      "⚠️ Qualidade dos dados precisa de atenção (87%)"
    ]
  },
  "performance": {
    "discoveryDuration": 2341.2,
    "contextCollectionDuration": 4532.1,
    "aiGenerationDuration": 12456.7,
    "translationDuration": 3241.5,
    "totalDuration": 23456.8,
    "rating": "GOOD"
  }
}
```

**Capacidades Únicas**:
- 🔍 **Descoberta automática completa** do banco (todas as tabelas + relacionamentos)
- 🧠 **Contexto rico multi-tabela** (amostras estratégicas preservando relacionamentos)
- 🤖 **15 validações cruzadas** geradas por GPT-4 baseadas no contexto completo
- 🔄 **Tradução Natural→SQL** com templates + fallback IA
- ⚡ **Execução paralela** otimizada
- 📊 **Dashboard automático** com gráficos inteligentes
- 🎯 **Insights e recomendações** acionáveis

#### **POST** `/api/enhanced-data-quality/discover-schema`
**Descoberta Enhanced Isolada**

Executa apenas a descoberta automática do schema com capacidades expandidas:

**Output**:
```json
{
  "discovery": {
    "databaseName": "sistema_docs",
    "metrics": {
      "totalTables": 47,
      "totalColumns": 423,
      "declaredFKs": 23,
      "implicitRelations": 12,
      "statisticalRelations": 8,
      "joinPatterns": 15
    }
  },
  "schema": {
    "tables": [
      {
        "fullName": "public.s_documentos",
        "columnCount": 12,
        "estimatedRows": 11377318,
        "tableSize": "2.3 GB",
        "hasPrimaryKey": true,
        "dataQualityScore": 87.3,
        "columns": [
          {
            "name": "id",
            "dataType": "integer",
            "isPrimaryKey": true,
            "classification": "IDENTIFIER",
            "nullFraction": 0.0
          }
        ]
      }
    ],
    "foreignKeys": [...],
    "implicitRelations": [
      {
        "source": { "table": "s_documentos", "column": "id_cliente" },
        "target": { "table": "clientes", "column": "id" },
        "confidence": 80.0,
        "method": "NAMING_PATTERN",
        "evidence": "Padrão de nomenclatura: id_cliente -> clientes.id"
      }
    ],
    "relevantRelations": [
      {
        "source": "public.s_documentos",
        "target": "public.clientes",
        "joinCondition": "s_documentos.id_cliente = clientes.id",
        "importance": 10,
        "type": "FK_DECLARED",
        "confidence": 100.0,
        "validationOpportunities": ["REFERENTIAL_INTEGRITY", "ORPHANED_RECORDS"]
      }
    ]
  }
}
```

#### **POST** `/api/enhanced-data-quality/generate-validations`
**Geração de Validações Sem Execução**

Para testar apenas a geração de validações IA:

**Input**:
```json
{
  "tableName": "s_documentos",
  "businessContext": "Documentos com workflow",
  "apiKey": "sk-...",
  "includeSQL": true
}
```

**Output**:
```json
{
  "generation": {
    "contextComplexity": "COMPLEX",
    "relatedTables": 8,
    "sampleSize": 350,
    "validationsGenerated": 15,
    "successfulTranslations": 14
  },
  "validations": [
    {
      "id": "uuid",
      "number": 1,
      "description": "Verificar se todos os documentos ATIVO têm clientes ATIVO relacionados",
      "type": "STATUS_CONSISTENCY",
      "priority": 9,
      "complexity": "MEDIUM",
      "involvedTables": ["s_documentos", "clientes"],
      "relevanceScore": 94.5,
      "isValidSQL": true,
      "translationMethod": "template",
      "sql": "SELECT COUNT(*) as total_active_docs, ..."
    }
  ],
  "insights": {
    "typeDistribution": {
      "STATUS_CONSISTENCY": 4,
      "REFERENTIAL_INTEGRITY": 3,
      "TEMPORAL_CONSISTENCY": 3,
      "UNIQUENESS": 2,
      "BUSINESS_RULE": 3
    },
    "keyInsights": [
      "Tipo de validação mais comum: STATUS_CONSISTENCY (4 validações)",
      "5 validações de alta prioridade identificadas"
    ]
  }
}
```

#### **GET** `/api/enhanced-data-quality/status`
**Status do Sistema Enhanced**

```json
{
  "status": "operational",
  "version": "1.0.0-enhanced",
  "capabilities": [
    "automatic_schema_discovery",
    "implicit_relationship_detection",
    "cross_table_context_collection",
    "ai_powered_validation_generation",
    "natural_to_sql_translation",
    "hybrid_execution_pipeline",
    "intelligent_visualization",
    "performance_monitoring"
  ],
  "performance": {
    "avgDiscoveryTime": "1-3 seconds",
    "avgContextCollectionTime": "2-5 seconds",
    "avgAIGenerationTime": "5-15 seconds",
    "avgTranslationTime": "3-10 seconds",
    "totalPipelineTime": "15-45 seconds"
  }
}
```

---

## 🔄 Comparação: Tradicional vs Enhanced

| Aspecto | Sistema Tradicional | Sistema Enhanced |
|---------|-------------------|------------------|
| **Descoberta** | Manual/básica | Automática completa |
| **Relacionamentos** | Só FKs declaradas | FKs + implícitos + estatísticos |
| **Validações** | Templates fixos | IA contextual (15 cruzadas) |
| **Contexto** | Tabela isolada | Multi-tabela com amostras |
| **Tradução SQL** | Manual | Natural→SQL automático |
| **Visualização** | Gráficos fixos | Dashboard inteligente |
| **Performance** | ~5-10s | ~15-45s (muito mais completo) |
| **Insights** | Básicos | Recomendações acionáveis |

---

## 🎯 Recomendações de Uso

### Para **Análise Rápida** (Sistema Tradicional):
```bash
# 1. Métricas básicas
POST /api/essential-metrics/collect

# 2. Métricas avançadas (padrões + outliers)
POST /api/essential-metrics/collect-advanced

# 3. Data quality com IA (10 regras)
POST /api/data-quality/analyze
```

### Para **Análise Completa** (Sistema Enhanced):
```bash
# Recomendado: Pipeline completo
POST /api/enhanced-data-quality/analyze-complete
{
  "tableName": "sua_tabela",
  "businessContext": "Contexto do seu negócio",
  "apiKey": "sua-openai-key"
}
```

### Para **Desenvolvimento/Debug**:
```bash
# Só descoberta
POST /api/enhanced-data-quality/discover-schema

# Só validações IA
POST /api/enhanced-data-quality/generate-validations
```

---

## 🚀 Diferencial Competitivo

O **Sistema Enhanced** oferece capacidades **únicas no mercado**:

1. **🔍 Zero-Setup Intelligence**: Descobre sozinho toda estrutura do banco
2. **🔗 Cross-Table Validation**: Validações entre tabelas relacionadas
3. **🧠 Hybrid AI Architecture**: Criatividade (GPT) + Precisão (MCP)
4. **📊 Smart Visualizations**: Gráficos automáticos baseados em padrões
5. **⚡ PostgreSQL Native**: Otimizado especificamente para PostgreSQL

**Exemplo de validação impossível no sistema tradicional**:
> *"Verificar se data_criacao do documento é posterior à data_nascimento do cliente relacionado via JOIN"*

**Resultado**: Sistema que **entende contexto** e gera insights **impossíveis** de conseguir manualmente!

---

## 📞 Suporte

- **Documentação Técnica**: `/docs`
- **Status da API**: `/health`
- **Logs**: Verifique os logs da aplicação para debug detalhado
- **Performance**: Métricas incluídas em todas as respostas Enhanced