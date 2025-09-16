#!/usr/bin/env bash
set -euo pipefail

OUTFILE="report.txt"

# Limpa saída antiga
rm -f "$OUTFILE"

echo "== Estrutura do projeto (até 4 níveis) ==" >> "$OUTFILE"
echo "" >> "$OUTFILE"
tree -L 4 >> "$OUTFILE"
echo "" >> "$OUTFILE"

echo "== Conteúdo dos arquivos ==" >> "$OUTFILE"
echo "" >> "$OUTFILE"

# Percorrer todos os arquivos de código e configuração
# (ignora binários comuns, .git, node_modules, obj, bin, .db, etc.)
find . \
  -type f \
  ! -path "*/.git/*" \
  ! -path "*/bin/*" \
  ! -path "*/obj/*" \
  ! -path "*/node_modules/*" \
  ! -path "*/.dbconnect/*" \
  ! -name "*.db" \
  | sort | while read -r f; do
    echo "------------------------------------------------------------" >> "$OUTFILE"
    echo "Arquivo: $f" >> "$OUTFILE"
    echo "------------------------------------------------------------" >> "$OUTFILE"
    cat "$f" >> "$OUTFILE" 2>/dev/null || echo "[erro ao ler $f]" >> "$OUTFILE"
    echo "" >> "$OUTFILE"
done

echo "Relatório salvo em $OUTFILE"
