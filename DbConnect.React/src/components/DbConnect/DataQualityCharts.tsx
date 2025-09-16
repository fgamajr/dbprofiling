import React from 'react';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Legend,
  AreaChart,
  Area,
  LineChart,
  Line
} from 'recharts';

interface HistogramBucket {
  rangeStart: number;
  rangeEnd: number;
  count: number;
  percentage: number;
}

interface BooleanStats {
  trueCount: number;
  falseCount: number;
  nullCount: number;
  truePercentage: number;
  falsePercentage: number;
  nullPercentage: number;
}

interface ValueFrequency {
  value: string;
  count: number;
  percentage: number;
}

interface TimelineBucket {
  period: string;
  count: number;
  percentage: number;
  label: string;
}

interface GeographicPoint {
  latitude: number;
  longitude: number;
  count: number;
  percentage: number;
  label: string;
}

interface DataQualityChartsProps {
  columnName: string;
  typeClassification: string;
  histogram?: HistogramBucket[];
  booleanStats?: BooleanStats;
  topValues?: ValueFrequency[];
  timeline?: TimelineBucket[];
  geographicPoints?: GeographicPoint[];
}

const COLORS = ['#3B82F6', '#EF4444', '#6B7280', '#10B981', '#F59E0B', '#8B5CF6'];

export function DataQualityCharts({
  columnName,
  typeClassification,
  histogram,
  booleanStats,
  topValues,
  timeline,
  geographicPoints
}: DataQualityChartsProps) {

  // Gráfico de Densidade para dados numéricos (curva suave)
  const renderDensityPlot = () => {
    if (!histogram || histogram.length === 0) return null;

    // Preparar dados para curva de densidade (usar contagem real)
    const totalCount = histogram.reduce((sum, bucket) => sum + bucket.count, 0);

    const data = histogram.map((bucket, index) => {
      // Usar o ponto médio do bucket para posicionamento no eixo X
      const midPoint = (bucket.rangeStart + bucket.rangeEnd) / 2;

      // CORREÇÃO: Densidade real baseada na contagem, não percentil artificial
      const density = totalCount > 0 ? (bucket.count / totalCount * 100) : 0;

      return {
        x: midPoint,
        density: density, // Densidade real baseada na frequência
        count: bucket.count,
        range: `${bucket.rangeStart.toFixed(0)} - ${bucket.rangeEnd.toFixed(0)}`,
        label: formatValue(midPoint)
      };
    });

    // Adicionar pontos nas extremidades para suavizar a curva
    const firstPoint = data[0];
    const lastPoint = data[data.length - 1];

    if (firstPoint && lastPoint) {
      // Ponto inicial (antes do primeiro bucket)
      data.unshift({
        x: firstPoint.x - (data[1]?.x - firstPoint.x || firstPoint.x * 0.1),
        density: 0,
        count: 0,
        range: 'início',
        label: '...'
      });

      // Ponto final (depois do último bucket)
      data.push({
        x: lastPoint.x + (lastPoint.x - data[data.length - 2]?.x || lastPoint.x * 0.1),
        density: 0,
        count: 0,
        range: 'fim',
        label: '...'
      });
    }

    return (
      <div className="bg-blue-50 rounded-lg p-4">
        <h7 className="font-medium text-blue-800 mb-3 block">📈 Distribuição de Densidade</h7>
        <ResponsiveContainer width="100%" height={200}>
          <AreaChart data={data}>
            <defs>
              <linearGradient id="densityGradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor="#3B82F6" stopOpacity={0.8}/>
                <stop offset="95%" stopColor="#3B82F6" stopOpacity={0.1}/>
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke="#E5E7EB" />
            <XAxis
              dataKey="x"
              type="number"
              scale="linear"
              fontSize={10}
              tickFormatter={(value) => formatValue(value)}
              domain={['dataMin', 'dataMax']}
            />
            <YAxis
              fontSize={11}
              label={{ value: 'Densidade (%)', angle: -90, position: 'insideLeft' }}
            />
            <Tooltip
              labelStyle={{ color: '#1f2937' }}
              formatter={(value, name) => [
                name === 'density' ? `${value}%` : `${value} registros`,
                name === 'density' ? 'Densidade' : 'Quantidade'
              ]}
              labelFormatter={(value) => `Valor: ${formatValue(value)}`}
            />
            <Area
              type="monotone"
              dataKey="density"
              stroke="#3B82F6"
              strokeWidth={2}
              fill="url(#densityGradient)"
              dot={false}
              activeDot={{ r: 4, fill: '#1D4ED8' }}
            />
          </AreaChart>
        </ResponsiveContainer>

        {/* Estatísticas resumidas */}
        <div className="mt-2 text-xs text-blue-700 bg-blue-100 p-2 rounded">
          💡 <strong>Curva de densidade:</strong> Mostra onde os valores se concentram.
          Picos altos = muitos registros nessa faixa. Vales baixos = poucos registros.
        </div>
      </div>
    );
  };

  // Função auxiliar para formatar valores no eixo X
  const formatValue = (value: number): string => {
    if (value >= 1000000) {
      return `${(value / 1000000).toFixed(1)}M`;
    }
    if (value >= 1000) {
      return `${(value / 1000).toFixed(1)}K`;
    }
    return value.toFixed(0);
  };

  // Gráfico de Pizza para dados booleanos
  const renderBooleanChart = () => {
    if (!booleanStats) return null;

    const data = [
      { name: 'True', value: booleanStats.trueCount, percentage: booleanStats.truePercentage, color: '#10B981' },
      { name: 'False', value: booleanStats.falseCount, percentage: booleanStats.falsePercentage, color: '#EF4444' },
    ];

    // Adicionar nulos apenas se existirem
    if (booleanStats.nullCount > 0) {
      data.push({ name: 'Null', value: booleanStats.nullCount, percentage: booleanStats.nullPercentage, color: '#6B7280' });
    }

    return (
      <div className="bg-green-50 rounded-lg p-4">
        <h7 className="font-medium text-green-800 mb-3 block">🔘 Distribuição Booleana</h7>
        <div className="grid grid-cols-2 gap-4">
          {/* Gráfico de Pizza */}
          <ResponsiveContainer width="100%" height={150}>
            <PieChart>
              <Pie
                data={data}
                cx="50%"
                cy="50%"
                innerRadius={25}
                outerRadius={60}
                paddingAngle={2}
                dataKey="value"
              >
                {data.map((entry, index) => (
                  <Cell key={`cell-${index}`} fill={entry.color} />
                ))}
              </Pie>
              <Tooltip formatter={(value) => [`${value} registros`, 'Quantidade']} />
            </PieChart>
          </ResponsiveContainer>

          {/* Estatísticas detalhadas */}
          <div className="space-y-2 text-sm">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 bg-green-500 rounded-full"></div>
                <span>True</span>
              </div>
              <div className="text-right">
                <div className="font-medium">{booleanStats.trueCount.toLocaleString()}</div>
                <div className="text-xs text-gray-600">{booleanStats.truePercentage.toFixed(1)}%</div>
              </div>
            </div>

            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 bg-red-500 rounded-full"></div>
                <span>False</span>
              </div>
              <div className="text-right">
                <div className="font-medium">{booleanStats.falseCount.toLocaleString()}</div>
                <div className="text-xs text-gray-600">{booleanStats.falsePercentage.toFixed(1)}%</div>
              </div>
            </div>

            {booleanStats.nullCount > 0 && (
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <div className="w-3 h-3 bg-gray-500 rounded-full"></div>
                  <span>Null</span>
                </div>
                <div className="text-right">
                  <div className="font-medium">{booleanStats.nullCount.toLocaleString()}</div>
                  <div className="text-xs text-gray-600">{booleanStats.nullPercentage.toFixed(1)}%</div>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    );
  };

  // Timeline para dados temporais (datas)
  const renderTimeline = () => {
    if (!timeline || timeline.length === 0) return null;

    // Preparar dados para o gráfico de linha temporal
    const data = timeline.map(bucket => ({
      period: bucket.period,
      count: bucket.count,
      percentage: bucket.percentage,
      label: bucket.label,
      // Converter string de data para timestamp para o eixo X
      timestamp: new Date(bucket.period).getTime()
    }));

    return (
      <div className="bg-purple-50 rounded-lg p-4">
        <h7 className="font-medium text-purple-800 mb-3 block">📅 Timeline Temporal</h7>
        <ResponsiveContainer width="100%" height={200}>
          <LineChart data={data}>
            <CartesianGrid strokeDasharray="3 3" stroke="#E5E7EB" />
            <XAxis
              dataKey="label"
              fontSize={10}
              angle={-45}
              textAnchor="end"
              height={60}
            />
            <YAxis
              fontSize={11}
              label={{ value: 'Registros', angle: -90, position: 'insideLeft' }}
            />
            <Tooltip
              labelFormatter={(label) => `Período: ${label}`}
              formatter={(value, name) => [
                name === 'count' ? `${value} registros` : `${value}%`,
                name === 'count' ? 'Quantidade' : 'Percentual'
              ]}
            />
            <Line
              type="monotone"
              dataKey="count"
              stroke="#8B5CF6"
              strokeWidth={3}
              dot={{ fill: '#8B5CF6', strokeWidth: 2, r: 4 }}
              activeDot={{ r: 6, fill: '#7C3AED' }}
            />
          </LineChart>
        </ResponsiveContainer>

        {/* Estatísticas resumidas */}
        <div className="mt-2 text-xs text-purple-700 bg-purple-100 p-2 rounded">
          💡 <strong>Timeline temporal:</strong> Mostra a distribuição de registros ao longo do tempo.
          Picos indicam períodos com mais atividade.
        </div>
      </div>
    );
  };

  // Visualização de mapa para coordenadas geográficas
  const renderGeographicMap = () => {
    if (!geographicPoints || geographicPoints.length === 0) return null;

    // Por enquanto, mostrar uma lista dos pontos mais frequentes
    // TODO: Implementar mapa real com biblioteca como Leaflet
    const topPoints = geographicPoints.slice(0, 10);

    return (
      <div className="bg-green-50 rounded-lg p-4">
        <h7 className="font-medium text-green-800 mb-3 block">🌍 Pontos Geográficos Mais Frequentes</h7>

        <div className="space-y-2">
          {topPoints.map((point, index) => (
            <div key={index} className="flex items-center justify-between text-sm">
              <div className="flex items-center gap-2">
                <div
                  className="w-3 h-3 rounded-full"
                  style={{ backgroundColor: COLORS[index % COLORS.length] }}
                ></div>
                <span className="font-mono text-gray-700">
                  {point.latitude.toFixed(4)}, {point.longitude.toFixed(4)}
                </span>
              </div>
              <div className="text-right">
                <div className="font-medium">{point.count.toLocaleString()}</div>
                <div className="text-xs text-gray-600">{point.percentage.toFixed(1)}%</div>
              </div>
            </div>
          ))}
        </div>

        <div className="mt-3 text-xs text-green-700 bg-green-100 p-2 rounded">
          🗺️ <strong>Coordenadas geográficas:</strong> Lista dos pontos mais frequentes.
          Em breve será exibido em mapa interativo.
        </div>
      </div>
    );
  };

  // Gráfico de barras para Top Valores (categóricas/texto)
  const renderTopValuesChart = () => {
    if (!topValues || topValues.length === 0) return null;

    // Mostrar apenas os top 5 para não poluir o gráfico
    const topData = topValues.slice(0, 5).map((item, index) => ({
      value: item.value.length > 15 ? item.value.substring(0, 15) + '...' : item.value,
      fullValue: item.value,
      count: item.count,
      percentage: item.percentage,
      color: COLORS[index % COLORS.length]
    }));

    return (
      <div className="bg-orange-50 rounded-lg p-4">
        <h7 className="font-medium text-orange-800 mb-3 block">🏆 Top 5 Valores Mais Frequentes</h7>
        <ResponsiveContainer width="100%" height={200}>
          <BarChart
            data={topData}
            layout="horizontal"
            margin={{ top: 5, right: 30, left: 60, bottom: 5 }}
          >
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis type="number" fontSize={10} />
            <YAxis
              type="category"
              dataKey="value"
              fontSize={10}
              width={60}
            />
            <Tooltip
              labelFormatter={(label) => {
                const item = topData.find(d => d.value === label);
                return item ? item.fullValue : label;
              }}
              formatter={(value, name) => [
                name === 'count' ? `${value} registros` : `${value}%`,
                name === 'count' ? 'Quantidade' : 'Percentual'
              ]}
            />
            <Bar dataKey="count" fill="#F59E0B" radius={[0, 2, 2, 0]} />
          </BarChart>
        </ResponsiveContainer>

        {/* Lista textual dos valores */}
        <div className="mt-3 space-y-1 text-xs">
          {topValues.slice(0, 5).map((item, index) => (
            <div key={index} className="flex items-center justify-between">
              <span className="font-mono text-gray-700 truncate max-w-xs">
                "{item.value}"
              </span>
              <span className="text-gray-600">
                {item.count.toLocaleString()} ({item.percentage.toFixed(1)}%)
              </span>
            </div>
          ))}
        </div>
      </div>
    );
  };

  // Decidir qual gráfico mostrar baseado no tipo de coluna
  const renderChart = () => {
    switch (typeClassification) {
      case 'Numeric':
        return renderDensityPlot(); // Curva de densidade para dados numéricos
      case 'Boolean':
        return renderBooleanChart();
      case 'DateTime':
        return renderTimeline(); // Timeline para dados temporais
      case 'Geographic':
        return renderGeographicMap(); // Mapa para coordenadas geográficas
      case 'Categorical':
      case 'Text':
        return renderTopValuesChart();
      default:
        // Para IDs únicos ou outros tipos, mostrar top values se disponível
        if (topValues && topValues.length > 0) {
          return renderTopValuesChart();
        }
        return null;
    }
  };

  const chart = renderChart();

  if (!chart) {
    return null;
  }

  return (
    <div className="mt-4">
      {chart}
    </div>
  );
}