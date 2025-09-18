import React, { useState, useEffect } from 'react';
import {
  Brain,
  Zap,
  Database,
  Search,
  CheckCircle,
  AlertTriangle,
  Clock,
  TrendingUp,
  Activity,
  Settings,
  RefreshCw,
  FileText,
  Network,
  BarChart3
} from 'lucide-react';
import { LoadingSpinner } from './LoadingSpinner';
import { Toast } from './Toast';
import { apiService } from '../../services/api';
import type {
  EnhancedStatusResponse,
  SchemaDiscoveryResponse,
  ValidationGenerationRequest,
  ValidationGenerationResponse,
  EnhancedAnalysisRequest,
  CompleteAnalysisResponse
} from '../../services/api';

interface EnhancedDataQualityProps {
  isConnected: boolean;
  activeProfileId: number | null;
}

export function EnhancedDataQuality({ isConnected, activeProfileId }: EnhancedDataQualityProps) {
  const [status, setStatus] = useState<EnhancedStatusResponse | null>(null);
  const [schemaDiscovery, setSchemaDiscovery] = useState<SchemaDiscoveryResponse | null>(null);
  const [validationGeneration, setValidationGeneration] = useState<ValidationGenerationResponse | null>(null);
  const [completeAnalysis, setCompleteAnalysis] = useState<CompleteAnalysisResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [discoveryLoading, setDiscoveryLoading] = useState(false);
  const [validationLoading, setValidationLoading] = useState(false);
  const [analysisLoading, setAnalysisLoading] = useState(false);
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'warning'; message: string } | null>(null);
  const [selectedTable, setSelectedTable] = useState('');
  const [businessContext, setBusinessContext] = useState('');
  const [apiKey, setApiKey] = useState('');
  const [availableTables, setAvailableTables] = useState<Array<{schema: string; name: string; fullName: string; size: string; rows: number}>>([]);
  const [hasApiKey, setHasApiKey] = useState(false);
  const [dataDictionary, setDataDictionary] = useState<File | null>(null);
  const [autoDiscoveryStarted, setAutoDiscoveryStarted] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage] = useState(10);

  useEffect(() => {
    if (isConnected && !autoDiscoveryStarted) {
      setAutoDiscoveryStarted(true);
      loadEnhancedStatus();
      loadAvailableTables();
      checkApiKeyStatus();
      // Automatically start schema discovery only once
      setTimeout(() => {
        handleDiscoverSchema();
      }, 1000); // Small delay to ensure everything is loaded
    }
  }, [isConnected]);

  const loadEnhancedStatus = async () => {
    setLoading(true);
    try {
      const response = await apiService.getEnhancedStatus();
      if (response?.success) {
        setStatus(response);
      } else {
        setToast({ type: 'error', message: 'Failed to load enhanced status' });
      }
    } catch (error) {
      setToast({ type: 'error', message: 'Error loading enhanced status' });
    } finally {
      setLoading(false);
    }
  };

  const loadAvailableTables = async () => {
    try {
      const response = await apiService.getDatabaseTables();
      if (response?.tables) {
        const formattedTables = response.tables.map(table => ({
          schema: table.schema,
          name: table.name,
          fullName: `${table.schema}.${table.name}`,
          size: table.size,
          rows: table.estimatedRows
        }));
        setAvailableTables(formattedTables);
      }
    } catch (error) {
      console.error('Failed to load available tables:', error);
      setToast({ type: 'error', message: 'Failed to load available tables' });
    }
  };

  const checkApiKeyStatus = async () => {
    // For now, we'll check if there's an API key in localStorage or similar
    // In a real implementation, you'd call an API endpoint to validate the key
    const savedApiKey = localStorage.getItem('openai_api_key') || apiKey;
    setHasApiKey(!!savedApiKey && savedApiKey.length > 10);
  };

  const handleApiKeyChange = (key: string) => {
    setApiKey(key);
    localStorage.setItem('openai_api_key', key);
    setHasApiKey(!!key && key.length > 10);
  };

  const handleDataDictionaryUpload = (file: File | null) => {
    setDataDictionary(file);
    if (file) {
      setToast({ type: 'success', message: `Data dictionary uploaded: ${file.name}` });
    }
  };

  const handleDiscoverSchema = async () => {
    setDiscoveryLoading(true);
    try {
      const response = await apiService.discoverSchema();
      if (response?.success) {
        console.log('Schema discovery response:', response); // Debug log
        setSchemaDiscovery(response);
        setToast({ type: 'success', message: `Schema discovered: ${response.discovery?.metrics?.totalTables || 0} tables found!` });
      } else {
        setToast({ type: 'error', message: response?.message || 'Schema discovery failed' });
      }
    } catch (error) {
      console.error('Schema discovery error:', error); // Debug log
      setToast({ type: 'error', message: 'Error during schema discovery' });
    } finally {
      setDiscoveryLoading(false);
    }
  };

  const handleGenerateValidations = async () => {
    if (!selectedTable.trim()) {
      setToast({ type: 'warning', message: 'Please enter a table name' });
      return;
    }

    setValidationLoading(true);
    try {
      const request: ValidationGenerationRequest = {
        tableName: selectedTable.trim(),
        businessContext: businessContext.trim() || undefined,
        apiKey: apiKey.trim() || undefined,
        includeSQL: true
      };

      const response = await apiService.generateValidations(request);
      if (response?.success) {
        setValidationGeneration(response);
        setToast({ type: 'success', message: `Generated ${response.generation.validationsGenerated} AI validations!` });
      } else {
        setToast({ type: 'error', message: response?.message || 'Validation generation failed' });
      }
    } catch (error) {
      setToast({ type: 'error', message: 'Error generating validations' });
    } finally {
      setValidationLoading(false);
    }
  };

  const handleCompleteAnalysis = async () => {
    if (!selectedTable.trim()) {
      setToast({ type: 'warning', message: 'Please enter a table name' });
      return;
    }

    setAnalysisLoading(true);
    try {
      const request: EnhancedAnalysisRequest = {
        tableName: selectedTable.trim(),
        businessContext: businessContext.trim() || undefined,
        apiKey: apiKey.trim() || undefined,
        includeSQL: false
      };

      const response = await apiService.analyzeComplete(request);
      if (response?.success) {
        setCompleteAnalysis(response);
        setToast({
          type: 'success',
          message: `Analysis complete! ${response.analysis.issuesDetected} issues detected with ${response.analysis.averageQuality}% quality`
        });
      } else {
        setToast({ type: 'error', message: response?.message || 'Complete analysis failed' });
      }
    } catch (error) {
      setToast({ type: 'error', message: 'Error during complete analysis' });
    } finally {
      setAnalysisLoading(false);
    }
  };

  if (!isConnected) {
    return (
      <div className="bg-white rounded-lg shadow p-6">
        <div className="text-center text-gray-500">
          <Database className="mx-auto h-12 w-12 mb-4" />
          <h3 className="text-lg font-medium mb-2">Enhanced AI Data Quality</h3>
          <p>Connect to a database to access enhanced AI profiling features</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {toast && (
        <Toast
          type={toast.type}
          message={toast.message}
          onClose={() => setToast(null)}
        />
      )}

      {/* Enhanced Status Card */}
      <div className="bg-white rounded-lg shadow">
        <div className="p-6">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-xl font-semibold flex items-center">
              <Brain className="mr-2 h-6 w-6 text-purple-600" />
              Enhanced AI Data Quality System
            </h2>
            <button
              onClick={loadEnhancedStatus}
              disabled={loading}
              className="flex items-center px-3 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 disabled:opacity-50"
            >
              {loading ? <LoadingSpinner size="sm" /> : <RefreshCw className="h-4 w-4" />}
              <span className="ml-2">Refresh Status</span>
            </button>
          </div>

          {status && (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
              <div className="bg-green-50 p-4 rounded-lg">
                <div className="flex items-center">
                  <CheckCircle className="h-8 w-8 text-green-600" />
                  <div className="ml-3">
                    <p className="text-sm font-medium text-green-900">Status</p>
                    <p className="text-lg font-semibold text-green-700">{status.status}</p>
                  </div>
                </div>
              </div>

              <div className="bg-blue-50 p-4 rounded-lg">
                <div className="flex items-center">
                  <Activity className="h-8 w-8 text-blue-600" />
                  <div className="ml-3">
                    <p className="text-sm font-medium text-blue-900">Version</p>
                    <p className="text-lg font-semibold text-blue-700">{status.version}</p>
                  </div>
                </div>
              </div>

              <div className="bg-purple-50 p-4 rounded-lg">
                <div className="flex items-center">
                  <Zap className="h-8 w-8 text-purple-600" />
                  <div className="ml-3">
                    <p className="text-sm font-medium text-purple-900">Capabilities</p>
                    <p className="text-lg font-semibold text-purple-700">{status.capabilities.length}</p>
                  </div>
                </div>
              </div>

              <div className="bg-yellow-50 p-4 rounded-lg">
                <div className="flex items-center">
                  <Clock className="h-8 w-8 text-yellow-600" />
                  <div className="ml-3">
                    <p className="text-sm font-medium text-yellow-900">Pipeline Time</p>
                    <p className="text-lg font-semibold text-yellow-700">{status.performance.totalPipelineTime}</p>
                  </div>
                </div>
              </div>
            </div>
          )}

          {status && (
            <div className="mb-6">
              <h3 className="text-lg font-medium mb-3">System Capabilities</h3>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                {status.capabilities.map((capability, index) => (
                  <div key={index} className="flex items-center text-sm">
                    <CheckCircle className="h-4 w-4 text-green-500 mr-2" />
                    <span className="text-gray-700">{capability.replace(/_/g, ' ')}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Schema Discovery Results - Auto-loaded */}
      <div className="bg-white rounded-lg shadow p-6">
        <h3 className="text-lg font-medium mb-4 flex items-center">
          <Network className="h-5 w-5 text-blue-600 mr-2" />
          Schema Discovery Results
          {discoveryLoading && <LoadingSpinner size="sm" className="ml-2" />}
        </h3>

        {discoveryLoading ? (
          <div className="flex items-center justify-center py-12">
            <div className="text-center">
              <LoadingSpinner size="lg" />
              <p className="text-gray-600 mt-4">Descobrindo schema do banco de dados...</p>
              <p className="text-sm text-gray-500 mt-2">Analisando tabelas, relacionamentos e metadados</p>
            </div>
          </div>
        ) : schemaDiscovery ? (
          <div>
            <div className="grid grid-cols-2 md:grid-cols-6 gap-4 mb-4">
              <div className="text-center p-3 bg-blue-50 rounded-lg">
                <div className="text-2xl font-bold text-blue-600">{schemaDiscovery.discovery?.metrics?.totalTables || 0}</div>
                <div className="text-sm text-blue-800">Tables</div>
              </div>
              <div className="text-center p-3 bg-green-50 rounded-lg">
                <div className="text-2xl font-bold text-green-600">{schemaDiscovery.discovery?.metrics?.totalColumns || 0}</div>
                <div className="text-sm text-green-800">Columns</div>
              </div>
              <div className="text-center p-3 bg-purple-50 rounded-lg">
                <div className="text-2xl font-bold text-purple-600">{schemaDiscovery.discovery?.metrics?.declaredFKs || 0}</div>
                <div className="text-sm text-purple-800">Declared FKs</div>
              </div>
              <div className="text-center p-3 bg-orange-50 rounded-lg">
                <div className="text-2xl font-bold text-orange-600">{schemaDiscovery.discovery?.metrics?.implicitRelations || 0}</div>
                <div className="text-sm text-orange-800">Implicit Relations</div>
              </div>
              <div className="text-center p-3 bg-red-50 rounded-lg">
                <div className="text-2xl font-bold text-red-600">{schemaDiscovery.discovery?.metrics?.statisticalRelations || 0}</div>
                <div className="text-sm text-red-800">Statistical Relations</div>
              </div>
              <div className="text-center p-3 bg-indigo-50 rounded-lg">
                <div className="text-2xl font-bold text-indigo-600">{schemaDiscovery.discovery?.metrics?.joinPatterns || 0}</div>
                <div className="text-sm text-indigo-800">Join Patterns</div>
              </div>
            </div>

            {schemaDiscovery.schema?.tables && schemaDiscovery.schema.tables.length > 0 && (() => {
              const totalTables = schemaDiscovery.schema.tables.length;
              const totalPages = Math.ceil(totalTables / itemsPerPage);
              const startIndex = (currentPage - 1) * itemsPerPage;
              const endIndex = startIndex + itemsPerPage;
              const currentTables = schemaDiscovery.schema.tables.slice(startIndex, endIndex);

              return (
                <div className="overflow-x-auto">
                  <table className="min-w-full divide-y divide-gray-200">
                    <thead className="bg-gray-50">
                      <tr>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Table</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Columns</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Est. Rows</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Size</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Quality</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Breakdown</th>
                      </tr>
                    </thead>
                    <tbody className="bg-white divide-y divide-gray-200">
                      {currentTables.map((table, index) => (
                      <tr key={table?.fullName || index} className="hover:bg-gray-50">
                        <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">
                          {table?.fullName || 'N/A'}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                          {table?.columnCount || 0}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                          {table?.estimatedRows?.toLocaleString() || 'N/A'}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                          {table?.tableSize || 'N/A'}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                          <div className="flex items-center">
                            <div className={`w-2 h-2 rounded-full mr-2 ${
                              (table?.dataQualityScore || 0) >= 90 ? 'bg-green-500' :
                              (table?.dataQualityScore || 0) >= 70 ? 'bg-yellow-500' : 'bg-red-500'
                            }`}></div>
                            {Math.round(table?.dataQualityScore || 0)}%
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                          <div
                            className="cursor-help text-base font-mono"
                            title={table?.qualityBreakdown?.detailedTooltip || 'No breakdown available'}
                          >
                            {table?.qualityBreakdown?.summary || 'N/A'}
                          </div>
                        </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>

                  {/* Pagination Controls */}
                  <div className="flex items-center justify-between mt-4">
                    <div className="text-sm text-gray-500">
                      Mostrando {startIndex + 1} a {Math.min(endIndex, totalTables)} de {totalTables} tabelas
                    </div>
                    <div className="flex items-center space-x-2">
                      <button
                        onClick={() => setCurrentPage(prev => Math.max(prev - 1, 1))}
                        disabled={currentPage === 1}
                        className="px-3 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        Anterior
                      </button>

                      <div className="flex items-center space-x-1">
                        {totalPages <= 10 ? (
                          // Show all pages if 10 or fewer
                          Array.from({ length: totalPages }, (_, i) => i + 1).map(page => (
                            <button
                              key={page}
                              onClick={() => setCurrentPage(page)}
                              className={`px-3 py-2 border rounded-md text-sm font-medium ${
                                currentPage === page
                                  ? 'border-blue-500 bg-blue-600 text-white'
                                  : 'border-gray-300 text-gray-700 bg-white hover:bg-gray-50'
                              }`}
                            >
                              {page}
                            </button>
                          ))
                        ) : (
                          // Show truncated pagination for more than 10 pages
                          <>
                            {currentPage > 3 && (
                              <>
                                <button onClick={() => setCurrentPage(1)} className="px-3 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50">1</button>
                                {currentPage > 4 && <span className="px-2 text-gray-500">...</span>}
                              </>
                            )}

                            {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                              const page = Math.max(1, Math.min(totalPages - 4, currentPage - 2)) + i;
                              return page <= totalPages ? (
                                <button
                                  key={page}
                                  onClick={() => setCurrentPage(page)}
                                  className={`px-3 py-2 border rounded-md text-sm font-medium ${
                                    currentPage === page
                                      ? 'border-blue-500 bg-blue-600 text-white'
                                      : 'border-gray-300 text-gray-700 bg-white hover:bg-gray-50'
                                  }`}
                                >
                                  {page}
                                </button>
                              ) : null;
                            })}

                            {currentPage < totalPages - 2 && (
                              <>
                                {currentPage < totalPages - 3 && <span className="px-2 text-gray-500">...</span>}
                                <button onClick={() => setCurrentPage(totalPages)} className="px-3 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50">{totalPages}</button>
                              </>
                            )}
                          </>
                        )}
                      </div>

                      <button
                        onClick={() => setCurrentPage(prev => Math.min(prev + 1, totalPages))}
                        disabled={currentPage === totalPages}
                        className="px-3 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        Próximo
                      </button>
                    </div>
                  </div>
                </div>
              );
            })()}
          </div>
        ) : (
          <div className="text-center py-8">
            <Network className="h-12 w-12 text-gray-400 mx-auto mb-4" />
            <p className="text-gray-600">Schema discovery não realizada</p>
            <button
              onClick={handleDiscoverSchema}
              className="mt-2 px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700"
            >
              Carregar Schema
            </button>
          </div>
        )}
      </div>

      {/* Input Form */}
      <div className="bg-white rounded-lg shadow p-6">
        <div className="mb-4">
          <h3 className="text-lg font-medium">Analysis Configuration</h3>
          <p className="text-sm text-gray-600 mt-1">
            Configure your table analysis. Start with Schema Discovery to see available tables.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Table Selection *
            </label>
            <select
              value={selectedTable}
              onChange={(e) => setSelectedTable(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">Select a table...</option>
              {availableTables.map((table) => (
                <option key={table.fullName} value={table.fullName}>
                  {table.fullName} ({table.rows.toLocaleString()} rows, {table.size})
                </option>
              ))}
            </select>
            <p className="text-xs text-gray-500 mt-1">
              {availableTables.length > 0
                ? `Choose from ${availableTables.length} available tables in your database.`
                : "Loading tables... Run Schema Discovery if needed."
              }
            </p>
          </div>
          <div className={`${!hasApiKey ? 'opacity-50' : ''}`}>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              API Key {hasApiKey ? '✅' : '⚠️'}
            </label>
            <input
              type="password"
              value={apiKey}
              onChange={(e) => handleApiKeyChange(e.target.value)}
              placeholder={hasApiKey ? "API key configured" : "Enter OpenAI/Claude API key"}
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <p className="text-xs text-gray-500 mt-1">
              {hasApiKey
                ? "✅ Valid API key detected. AI features are enabled."
                : "⚠️ API key required for AI-powered features."
              }
            </p>
          </div>
          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Business Context
            </label>
            <div className="space-y-3">
              <textarea
                value={businessContext}
                onChange={(e) => setBusinessContext(e.target.value)}
                placeholder="e.g., 'This table stores customer orders with payment information and shipping details. Focus on data integrity for financial compliance.'"
                rows={3}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              <div className="border-t border-gray-200 pt-3">
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  📄 Alternative: Upload Data Dictionary
                </label>
                <div className="flex items-center space-x-3">
                  <input
                    type="file"
                    accept=".txt,.csv,.md,.json,.yml,.yaml,.xml"
                    onChange={(e) => handleDataDictionaryUpload(e.target.files?.[0] || null)}
                    className="block w-full text-sm text-gray-500 file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:text-sm file:font-semibold file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100"
                  />
                  {dataDictionary && (
                    <button
                      onClick={() => handleDataDictionaryUpload(null)}
                      className="text-red-600 hover:text-red-800"
                    >
                      Remove
                    </button>
                  )}
                </div>
                <p className="text-xs text-gray-500 mt-1">
                  Upload a data catalog or dictionary file as an alternative to manual business context.
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Quick Start Guide */}
        <div className="mt-4 p-3 bg-blue-50 rounded-md border border-blue-200">
          <h4 className="text-sm font-medium text-blue-900 mb-2">🚀 Quick Start Guide:</h4>
          <ol className="text-xs text-blue-800 space-y-1">
            <li>1. Schema discovery runs automatically when you access this tab</li>
            <li>2. Select a table from the dropdown (populated from discovered tables)</li>
            <li>3. Use "Generate AI Validations" for intelligent data quality rules</li>
            <li>4. Run "Complete Analysis" for comprehensive insights</li>
          </ol>
        </div>
      </div>

      {/* Action Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">

        {/* AI Validation Generation */}
        <div className={`bg-white rounded-lg shadow p-6 ${!hasApiKey ? 'opacity-60' : ''}`}>
          <div className="flex items-center mb-4">
            <Brain className={`h-6 w-6 mr-2 ${hasApiKey ? 'text-purple-600' : 'text-gray-400'}`} />
            <h3 className="text-lg font-medium">AI Validations {!hasApiKey ? '🔒' : ''}</h3>
          </div>
          <p className="text-gray-600 mb-4">
            Generate intelligent data quality validations using AI
          </p>
          <button
            onClick={handleGenerateValidations}
            disabled={validationLoading || !hasApiKey}
            className={`w-full flex items-center justify-center px-4 py-2 rounded-md disabled:opacity-50 ${
              hasApiKey
                ? 'bg-purple-600 text-white hover:bg-purple-700'
                : 'bg-gray-300 text-gray-500 cursor-not-allowed'
            }`}
            title={!hasApiKey ? 'API key required for AI features' : ''}
          >
            {validationLoading ? <LoadingSpinner size="sm" /> : <Brain className="h-4 w-4 mr-2" />}
            {!hasApiKey ? 'API Key Required' : 'Generate Validations'}
          </button>
        </div>

        {/* Complete Analysis */}
        <div className={`bg-white rounded-lg shadow p-6 ${!hasApiKey ? 'opacity-60' : ''}`}>
          <div className="flex items-center mb-4">
            <TrendingUp className={`h-6 w-6 mr-2 ${hasApiKey ? 'text-green-600' : 'text-gray-400'}`} />
            <h3 className="text-lg font-medium">Complete Analysis {!hasApiKey ? '🔒' : ''}</h3>
          </div>
          <p className="text-gray-600 mb-4">
            Full AI-powered analysis with dashboard and insights
          </p>
          <button
            onClick={handleCompleteAnalysis}
            disabled={analysisLoading || !hasApiKey}
            className={`w-full flex items-center justify-center px-4 py-2 rounded-md disabled:opacity-50 ${
              hasApiKey
                ? 'bg-green-600 text-white hover:bg-green-700'
                : 'bg-gray-300 text-gray-500 cursor-not-allowed'
            }`}
            title={!hasApiKey ? 'API key required for AI features' : ''}
          >
            {analysisLoading ? <LoadingSpinner size="sm" /> : <TrendingUp className="h-4 w-4 mr-2" />}
            {!hasApiKey ? 'API Key Required' : 'Analyze Complete'}
          </button>
        </div>
      </div>


      {validationGeneration && (
        <div className="bg-white rounded-lg shadow p-6">
          <h3 className="text-lg font-medium mb-4 flex items-center">
            <Brain className="h-5 w-5 text-purple-600 mr-2" />
            AI Generated Validations
          </h3>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
            <div className="text-center">
              <div className="text-2xl font-bold text-purple-600">{validationGeneration.generation.validationsGenerated}</div>
              <div className="text-sm text-gray-600">Generated</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold text-green-600">{validationGeneration.generation.successfulTranslations}</div>
              <div className="text-sm text-gray-600">Translated</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold text-blue-600">{validationGeneration.generation.relatedTables}</div>
              <div className="text-sm text-gray-600">Related Tables</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold text-yellow-600">{validationGeneration.generation.sampleSize}</div>
              <div className="text-sm text-gray-600">Sample Size</div>
            </div>
          </div>
          <div className="max-h-64 overflow-y-auto">
            <div className="space-y-3">
              {validationGeneration.validations.map((validation, index) => (
                <div key={index} className="border rounded-lg p-4">
                  <div className="flex items-center justify-between mb-2">
                    <span className="text-sm font-medium text-gray-900">#{validation.number}</span>
                    <div className="flex items-center space-x-2">
                      <span className={`inline-flex px-2 py-1 text-xs font-semibold rounded-full ${
                        validation.priority >= 8 ? 'bg-red-100 text-red-800' :
                        validation.priority >= 5 ? 'bg-yellow-100 text-yellow-800' :
                        'bg-green-100 text-green-800'
                      }`}>
                        Priority {validation.priority}
                      </span>
                      <span className="inline-flex px-2 py-1 text-xs font-semibold rounded-full bg-blue-100 text-blue-800">
                        {validation.type}
                      </span>
                    </div>
                  </div>
                  <p className="text-sm text-gray-700 mb-2">{validation.description}</p>
                  <div className="text-xs text-gray-500">
                    <span>Relevance: {validation.relevanceScore}% | </span>
                    <span>Tables: {validation.involvedTables.join(', ')} | </span>
                    <span>Method: {validation.translationMethod}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {completeAnalysis && (
        <div className="bg-white rounded-lg shadow p-6">
          <h3 className="text-lg font-medium mb-4 flex items-center">
            <BarChart3 className="h-5 w-5 text-green-600 mr-2" />
            Complete Analysis Results
          </h3>
          <div className="grid grid-cols-2 md:grid-cols-5 gap-4 mb-6">
            <div className="text-center">
              <div className="text-2xl font-bold text-green-600">{completeAnalysis.analysis.averageQuality}%</div>
              <div className="text-sm text-gray-600">Quality Score</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold text-blue-600">{completeAnalysis.analysis.validationsExecuted}</div>
              <div className="text-sm text-gray-600">Validations</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold text-red-600">{completeAnalysis.analysis.issuesDetected}</div>
              <div className="text-sm text-gray-600">Issues Found</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold text-yellow-600">{completeAnalysis.summary.highPriorityIssues}</div>
              <div className="text-sm text-gray-600">High Priority</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold text-purple-600">{(completeAnalysis.performance.totalDuration / 1000).toFixed(1)}s</div>
              <div className="text-sm text-gray-600">Execution Time</div>
            </div>
          </div>

          {completeAnalysis.summary.recommendations.length > 0 && (
            <div className="mb-6">
              <h4 className="text-md font-medium mb-3">🎯 Recommendations</h4>
              <div className="space-y-2">
                {completeAnalysis.summary.recommendations.map((rec, index) => (
                  <div key={index} className="flex items-start">
                    <AlertTriangle className="h-4 w-4 text-yellow-500 mr-2 mt-0.5" />
                    <span className="text-sm text-gray-700">{rec}</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {completeAnalysis.dashboard.insights.length > 0 && (
            <div className="mb-6">
              <h4 className="text-md font-medium mb-3">💡 Key Insights</h4>
              <div className="space-y-2">
                {completeAnalysis.dashboard.insights.map((insight, index) => (
                  <div key={index} className="flex items-start">
                    <CheckCircle className="h-4 w-4 text-green-500 mr-2 mt-0.5" />
                    <span className="text-sm text-gray-700">{insight}</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          <div className="max-h-64 overflow-y-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Validation</th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Priority</th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Issues</th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Quality</th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {completeAnalysis.validations.map((validation, index) => (
                  <tr key={index}>
                    <td className="px-6 py-4 text-sm text-gray-900">{validation.description}</td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">{validation.priority}</td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <span className={`inline-flex px-2 py-1 text-xs font-semibold rounded-full ${
                        validation.status === 'PASS' ? 'bg-green-100 text-green-800' :
                        validation.status === 'ISSUES_FOUND' ? 'bg-yellow-100 text-yellow-800' :
                        'bg-red-100 text-red-800'
                      }`}>
                        {validation.status}
                      </span>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">{validation.issuesDetected.toLocaleString()}</td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">{validation.qualityPercentage}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}