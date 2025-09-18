// Enhanced AI Data Quality Interfaces
interface EnhancedStatusResponse {
  success: boolean;
  status: string;
  version: string;
  capabilities: string[];
  performance: {
    avgDiscoveryTime: string;
    avgContextCollectionTime: string;
    avgAIGenerationTime: string;
    avgTranslationTime: string;
    totalPipelineTime: string;
  };
}

interface SchemaDiscoveryResponse {
  success: boolean;
  message: string;
  discovery: {
    databaseName: string;
    discoveredAt: string;
    metrics: {
      totalTables: number;
      totalColumns: number;
      declaredFKs: number;
      implicitRelations: number;
      statisticalRelations: number;
      joinPatterns: number;
    };
  };
  schema: {
    tables: EnhancedTableInfo[];
    foreignKeys: ForeignKeyInfo[];
    implicitRelations: ImplicitRelationInfo[];
    relevantRelations: RelevantRelationInfo[];
  };
}

interface EnhancedTableInfo {
  fullName: string;
  schema: string;
  name: string;
  type: string;
  columnCount: number;
  estimatedRows: number;
  tableSize: string;
  hasPrimaryKey: boolean;
  dataQualityScore: number;
  columns: EnhancedColumnInfo[];
}

interface EnhancedColumnInfo {
  name: string;
  dataType: string;
  isNullable: boolean;
  isPrimaryKey: boolean;
  isForeignKey: boolean;
  foreignTable?: string;
  classification: string;
  distinctValues: number;
  nullFraction: number;
}

interface ForeignKeyInfo {
  source: { table: string; column: string };
  target: { table: string; column: string };
  constraintName: string;
}

interface ImplicitRelationInfo {
  source: { table: string; column: string };
  target: { table: string; column: string };
  confidence: number;
  method: string;
  evidence: string;
}

interface RelevantRelationInfo {
  source: string;
  target: string;
  joinCondition: string;
  importance: number;
  type: string;
  confidence: number;
  validationOpportunities: string[];
}

interface ValidationGenerationRequest {
  tableName: string;
  businessContext?: string;
  apiKey?: string;
  includeSQL?: boolean;
}

interface ValidationGenerationResponse {
  success: boolean;
  message: string;
  generation: {
    focusTable: string;
    contextComplexity: string;
    relatedTables: number;
    sampleSize: number;
    validationsGenerated: number;
    successfulTranslations: number;
  };
  validations: EnhancedValidation[];
  insights: {
    typeDistribution: Record<string, number>;
    keyInsights: string[];
  };
  performance: {
    discoveryDuration: number;
    contextCollectionDuration: number;
    aiGenerationDuration: number;
    translationDuration: number;
    totalDuration: number;
  };
}

interface EnhancedValidation {
  id: string;
  number: number;
  description: string;
  type: string;
  priority: number;
  complexity: string;
  involvedTables: string[];
  relevanceScore: number;
  isValidSQL: boolean;
  translationMethod: string;
  sql?: string;
}

interface EnhancedAnalysisRequest {
  tableName: string;
  businessContext?: string;
  apiKey?: string;
  includeSQL?: boolean;
}

interface CompleteAnalysisResponse {
  success: boolean;
  message: string;
  analysis: {
    focusTable: string;
    executionTime: number;
    validationsExecuted: number;
    issuesDetected: number;
    averageQuality: number;
    performanceRating: string;
  };
  summary: {
    totalValidations: number;
    successfulExecutions: number;
    failedExecutions: number;
    totalIssues: number;
    highPriorityIssues: number;
    mediumPriorityIssues: number;
    recommendations: string[];
  };
  validations: ExecutedValidation[];
  dashboard: EnhancedDashboard;
  performance: {
    discoveryDuration: number;
    contextCollectionDuration: number;
    aiGenerationDuration: number;
    translationDuration: number;
    totalDuration: number;
    rating: string;
  };
}

interface ExecutedValidation {
  id: string;
  description: string;
  priority: number;
  type: string;
  status: string;
  issuesDetected: number;
  totalRecords: number;
  qualityPercentage: number;
  executionDuration: number;
  sql?: string;
}

interface EnhancedDashboard {
  id: string;
  title: string;
  description: string;
  layout: any;
  visualizations: Visualization[];
  insights: string[];
}

interface Visualization {
  id: string;
  title: string;
  chartType: string;
  data: any;
  configuration: any;
  priority: number;
}

interface LoginRequest {
  username: string;
  password: string;
}

interface LoginResponse {
  ok: boolean;
  message?: string;
}

interface RegisterResponse {
  ok: boolean;
  message?: string;
}

interface User {
  username: string;
  id: number;
}

interface Profile {
  id: number;
  name: string;
  kind: number;
  hostOrFile: string;
  port?: number;
  database: string;
  username: string;
  createdAtUtc?: string;
}

interface CreateProfileRequest {
  name: string;
  kind: number;
  hostOrFile: string;
  port?: number;
  database: string;
  username: string;
  password: string;
}

interface DatabaseInfo {
  basic: {
    serverVersion: string;
    databaseName: string;
    databaseSize: string;
  };
  objects: {
    tablesCount: number;
    viewsCount: number;
    indexesCount: number;
    functionsCount: number;
    triggersCount: number;
  };
  performance: {
    activeConnections: number;
    uptime: string;
    cacheHitRatio: string;
  };
  topTables: Array<{
    schema: string;
    table: string;
    estimatedRows: number;
  }>;
  schemas: string[];
  collectedAt: string;
}

interface DatabaseTable {
  schema: string;
  name: string;
  estimatedRows: number;
  size: string;
  comment?: string;
}

interface DatabaseTablesResponse {
  tables: DatabaseTable[];
  totalTables: number;
  collectedAt: string;
}

interface TableColumn {
  name: string;
  dataType: string;
  maxLength?: number;
  nullable: boolean;
  defaultValue?: string;
  isPrimaryKey: boolean;
}

interface TableIndex {
  name: string;
  columns: string[];
  isUnique: boolean;
  isPrimary: boolean;
}

interface TableDetailsResponse {
  schema: string;
  tableName: string;
  columns: TableColumn[];
  indexes: TableIndex[];
  sampleData: Record<string, any>[];
  statistics: {
    totalRows: number;
  };
  collectedAt: string;
}

interface ColumnProfilingResponse {
  schema: string;
  tableName: string;
  columnName: string;
  columnInfo: {
    dataType: string;
    maxLength?: number;
    nullable: boolean;
    defaultValue?: string;
  };
  statistics: {
    totalRows: number;
    nullCount: number;
    nullPercentage: number;
    uniqueCount: number;
    uniquePercentage: number;
    topValues: Array<{
      value: any;
      frequency: number;
      percentage: number;
    }>;
  };
  patterns: {
    emailPattern?: {
      count: number;
      percentage: number;
      description: string;
    };
    cpfPattern?: {
      count: number;
      percentage: number;
      description: string;
    };
    cnpjPattern?: {
      count: number;
      percentage: number;
      description: string;
    };
    phonePattern?: {
      count: number;
      percentage: number;
      description: string;
    };
    stringLength?: {
      min: number;
      max: number;
      average: number;
      description: string;
    };
    dateRange?: {
      minDate: any;
      maxDate: any;
      description: string;
    };
    numericStats?: {
      min: any;
      max: any;
      average?: number;
      standardDeviation?: number;
      description: string;
    };
  };
  collectedAt: string;
}

interface TableProfilingResponse {
  schema: string;
  tableName: string;
  totalColumns: number;
  profiledColumns: number;
  columnsProfile: ColumnProfilingResponse[];
  collectedAt: string;
  note?: string;
}

class ApiService {
  private baseUrl = '/api';

  private async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const url = `${this.baseUrl}${endpoint}`;
    
    console.log('🌐 Making request to:', url, options.method || 'GET');
    
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...options.headers as Record<string, string>,
    };

    const response = await fetch(url, {
      ...options,
      headers,
      credentials: 'include', // Important for JWT cookies
    });

    console.log('🌐 Response status:', response.status, response.statusText);

    if (!response.ok) {
      const errorText = await response.text();
      console.log('🌐 Error response:', errorText);
      throw new Error(`HTTP ${response.status}: ${errorText}`);
    }

    const result = await response.json();
    console.log('🌐 Response data:', result);
    return result;
  }

  async register(username: string, password: string): Promise<{ ok: boolean; data: { message: string } }> {
    try {
      const response = await this.request<RegisterResponse>('/auth/register', {
        method: 'POST',
        body: JSON.stringify({ username, password }),
      });

      return {
        ok: response.ok,
        data: { message: response.message || 'Usuário criado com sucesso' }
      };
    } catch (error) {
      return {
        ok: false,
        data: { message: error instanceof Error ? error.message : 'Erro ao registrar usuário' }
      };
    }
  }

  async login(username: string, password: string): Promise<boolean> {
    console.log('🔐 Attempting login with:', { username, password: '***' });
    try {
      const response = await this.request<LoginResponse>('/auth/login', {
        method: 'POST',
        body: JSON.stringify({ username, password }),
      });

      console.log('🔐 Login response:', response);
      return response.ok;
    } catch (error) {
      console.error('🔐 Login error:', error);
      return false;
    }
  }

  async getMe(): Promise<User | null> {
    try {
      const response = await this.request<User>('/auth/me');
      return response;
    } catch (error) {
      return null;
    }
  }

  async logout(): Promise<boolean> {
    try {
      await this.request('/auth/logout', {
        method: 'POST',
      });
    } catch (error) {
      console.error('Logout error:', error);
    }
    return true;
  }

  async listProfiles(): Promise<Profile[]> {
    try {
      const response = await this.request<Profile[]>('/u/profiles');
      return response;
    } catch (error) {
      console.error('Failed to load profiles:', error);
      throw error;
    }
  }

  async createProfile(profile: CreateProfileRequest): Promise<Profile> {
    try {
      const response = await this.request<Profile>('/u/profiles', {
        method: 'POST',
        body: JSON.stringify(profile),
      });
      return response;
    } catch (error) {
      console.error('Failed to create profile:', error);
      throw error;
    }
  }

  async updateProfile(id: number, profile: Partial<CreateProfileRequest>): Promise<Profile> {
    try {
      const response = await this.request<Profile>(`/u/profiles/${id}`, {
        method: 'PUT',
        body: JSON.stringify(profile),
      });
      return response;
    } catch (error) {
      console.error('Failed to update profile:', error);
      throw error;
    }
  }

  async deleteProfile(id: number): Promise<void> {
    try {
      await this.request(`/u/profiles/${id}`, {
        method: 'DELETE',
      });
    } catch (error) {
      console.error('Failed to delete profile:', error);
      throw error;
    }
  }

  async testConnection(profileId: number): Promise<{ success: boolean; message: string }> {
    try {
      const response = await this.request<{ success: boolean; message: string }>(`/u/profiles/${profileId}/test`, {
        method: 'POST',
      });
      return response;
    } catch (error) {
      return {
        success: false,
        message: error instanceof Error ? error.message : 'Erro ao testar conexão'
      };
    }
  }

  async connectProfile(profileId: number): Promise<{ success: boolean; message: string; profileId?: number }> {
    try {
      const response = await this.request<{ message: string; profileId: number }>(`/u/profiles/${profileId}/connect`, {
        method: 'POST',
      });
      return {
        success: true,
        message: response.message,
        profileId: response.profileId
      };
    } catch (error) {
      return {
        success: false,
        message: error instanceof Error ? error.message : 'Erro ao conectar ao perfil'
      };
    }
  }

  async getActiveProfile(): Promise<{ activeProfileId: number | null }> {
    try {
      const response = await this.request<{ activeProfileId: number | null }>('/u/profiles/active');
      return response;
    } catch (error) {
      console.error('Failed to get active profile:', error);
      return { activeProfileId: null };
    }
  }

  async disconnectProfile(): Promise<{ success: boolean; message: string }> {
    try {
      const response = await this.request<{ message: string }>('/u/profiles/disconnect', {
        method: 'POST',
      });
      return {
        success: true,
        message: response.message
      };
    } catch (error) {
      return {
        success: false,
        message: error instanceof Error ? error.message : 'Erro ao desconectar'
      };
    }
  }

  async getDatabaseInfo(): Promise<DatabaseInfo | null> {
    try {
      const response = await this.request<DatabaseInfo>('/u/database/info');
      return response;
    } catch (error) {
      console.error('Failed to get database info:', error);
      return null;
    }
  }

  async getDatabaseTables(): Promise<DatabaseTablesResponse | null> {
    try {
      const response = await this.request<DatabaseTablesResponse>('/u/database/tables');
      return response;
    } catch (error) {
      console.error('Failed to get database tables:', error);
      return null;
    }
  }

  async getTableDetails(schema: string, tableName: string): Promise<TableDetailsResponse | null> {
    try {
      const response = await this.request<TableDetailsResponse>(`/u/database/tables/${encodeURIComponent(schema)}/${encodeURIComponent(tableName)}`);
      return response;
    } catch (error) {
      console.error('Failed to get table details:', error);
      return null;
    }
  }

  async getColumnProfiling(schema: string, tableName: string, columnName: string): Promise<ColumnProfilingResponse | null> {
    try {
      const response = await this.request<ColumnProfilingResponse>(
        `/u/database/tables/${encodeURIComponent(schema)}/${encodeURIComponent(tableName)}/columns/${encodeURIComponent(columnName)}/profile`
      );
      return response;
    } catch (error) {
      console.error('Failed to get column profiling:', error);
      return null;
    }
  }

  async getTableProfiling(schema: string, tableName: string): Promise<TableProfilingResponse | null> {
    try {
      const response = await this.request<TableProfilingResponse>(
        `/u/database/tables/${encodeURIComponent(schema)}/${encodeURIComponent(tableName)}/profile`
      );
      return response;
    } catch (error) {
      console.error('Failed to get table profiling:', error);
      return null;
    }
  }

  // Enhanced AI Data Quality APIs
  async getEnhancedStatus(): Promise<EnhancedStatusResponse | null> {
    try {
      const response = await this.request<EnhancedStatusResponse>('/enhanced-data-quality/status');
      return response;
    } catch (error) {
      console.error('Failed to get enhanced status:', error);
      return null;
    }
  }

  async discoverSchema(): Promise<SchemaDiscoveryResponse | null> {
    try {
      const response = await this.request<SchemaDiscoveryResponse>('/enhanced-data-quality/discover-schema', {
        method: 'POST'
      });
      return response;
    } catch (error) {
      console.error('Failed to discover schema:', error);
      return null;
    }
  }

  async generateValidations(request: ValidationGenerationRequest): Promise<ValidationGenerationResponse | null> {
    try {
      const response = await this.request<ValidationGenerationResponse>('/enhanced-data-quality/generate-validations', {
        method: 'POST',
        body: JSON.stringify(request)
      });
      return response;
    } catch (error) {
      console.error('Failed to generate validations:', error);
      return null;
    }
  }

  async analyzeComplete(request: EnhancedAnalysisRequest): Promise<CompleteAnalysisResponse | null> {
    try {
      const response = await this.request<CompleteAnalysisResponse>('/enhanced-data-quality/analyze-complete', {
        method: 'POST',
        body: JSON.stringify(request)
      });
      return response;
    } catch (error) {
      console.error('Failed to analyze complete:', error);
      return null;
    }
  }
}

export const apiService = new ApiService();
export type {
  User,
  Profile,
  CreateProfileRequest,
  DatabaseInfo,
  DatabaseTable,
  DatabaseTablesResponse,
  TableColumn,
  TableIndex,
  TableDetailsResponse,
  ColumnProfilingResponse,
  TableProfilingResponse,
  // Enhanced AI Data Quality Types
  EnhancedStatusResponse,
  SchemaDiscoveryResponse,
  ValidationGenerationRequest,
  ValidationGenerationResponse,
  EnhancedAnalysisRequest,
  CompleteAnalysisResponse,
  EnhancedTableInfo,
  EnhancedValidation,
  ExecutedValidation,
  EnhancedDashboard
};