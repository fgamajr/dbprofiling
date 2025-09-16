#!/usr/bin/env bash
set -euo pipefail

BRANCH="feat/profiles-crud-frontend"
WEB_HTML="DbConnect.Web/wwwroot/index.html"

echo "==> Verificando repositório git..."
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || { echo "Este diretório não é um repo git."; exit 1; }

# cria branch
if git show-ref --quiet refs/heads/"$BRANCH"; then
  git checkout "$BRANCH"
else
  git checkout -b "$BRANCH"
fi

echo "==> Patchando $WEB_HTML ..."

# backup
cp "$WEB_HTML" "$WEB_HTML.bak"

# injeta helpers e componentes
awk '
/const API = {/ && !seen_api {
    print $0
    print "    async updateProfile(id, dto){"
    print "      const r = await fetch(`/api/u/profiles/${id}`, { method:\"PUT\", headers:{\"Content-Type\":\"application/json\"}, credentials:\"include\", body: JSON.stringify(dto) });"
    print "      const d = await r.json().catch(()=>({}));"
    print "      if(!r.ok) throw new Error(d.message||\"Erro ao atualizar\");"
    print "      return d;"
    print "    },"
    print "    async deleteProfile(id){"
    print "      const r = await fetch(`/api/u/profiles/${id}`, { method:\"DELETE\", credentials:\"include\" });"
    print "      const d = await r.json().catch(()=>({}));"
    print "      if(!r.ok) throw new Error(d.message||\"Erro ao remover\");"
    print "      return d;"
    print "    },"
    print "    async testProfile(id){"
    print "      const r = await fetch(`/api/u/profiles/${id}/test`, { method:\"POST\", credentials:\"include\" });"
    print "      const d = await r.json().catch(()=>({}));"
    print "      if(!r.ok) throw new Error(d.message||\"Falha no teste\");"
    print "      return d;"
    print "    },"
    seen_api=1; next
}
{print}
' "$WEB_HTML" > "$WEB_HTML.tmp" && mv "$WEB_HTML.tmp" "$WEB_HTML"

# garante que EditProfileModal e ProfilesList estão presentes
grep -q "function EditProfileModal" "$WEB_HTML" || cat >> "$WEB_HTML" <<'EOF'

<!-- === Added by feature-profiles-crud-frontend.sh === -->
<script>
function EditProfileModal({open, onClose, profile, onSaved}){
  const [form,setForm]=React.useState(null);
  const [busy,setBusy]=React.useState(false);
  const [msg,setMsg]=React.useState(null);

  React.useEffect(()=>{
    if(!profile){ setForm(null); return; }
    setForm({...profile, password:""});
  },[profile]);

  if(!open||!form) return null;

  async function save(){
    setBusy(true); setMsg(null);
    try{
      const payload={...form};
      if(!payload.password) delete payload.password;
      await API.updateProfile(profile.id,payload);
      onSaved && onSaved();
      onClose();
    }catch(e){ setMsg(e.message); }
    finally{ setBusy(false); }
  }

  return (
    React.createElement("div",{className:"fixed inset-0 bg-black/30 flex items-center justify-center"},
      React.createElement("div",{className:"bg-white p-6 rounded-xl w-full max-w-lg"},
        [
          React.createElement("h3",{key:"t",className:"font-semibold mb-3"},"Editar Perfil"),
          msg && React.createElement("div",{key:"m",className:"text-red-600 mb-2"},msg),
          React.createElement("input",{key:"n",className:"input mb-2",value:form.name,onChange:e=>setForm(s=>({...s,name:e.target.value}))}),
          React.createElement("input",{key:"h",className:"input mb-2",value:form.hostOrFile,onChange:e=>setForm(s=>({...s,hostOrFile:e.target.value}))}),
          React.createElement("input",{key:"db",className:"input mb-2",value:form.database,onChange:e=>setForm(s=>({...s,database:e.target.value}))}),
          React.createElement("input",{key:"u",className:"input mb-2",value:form.username,onChange:e=>setForm(s=>({...s,username:e.target.value}))}),
          React.createElement("input",{key:"p",className:"input mb-2",type:"password",placeholder:"Nova senha",value:form.password,onChange:e=>setForm(s=>({...s,password:e.target.value}))}),
          React.createElement("div",{key:"b",className:"flex justify-end gap-2"},[
            React.createElement("button",{onClick:onClose,className:"btn"},"Cancelar"),
            React.createElement("button",{onClick:save,className:"btn-primary",disabled:busy},busy?"Salvando...":"Salvar & Testar")
          ])
        ]
      )
    )
  );
}

function ProfilesList({items,onConnect,onEdit,onDelete}){
  if(!items) return null;
  return React.createElement("div",{className:"space-y-3"},
    items.map(p=>
      React.createElement("div",{key:p.id,className:"p-3 bg-white rounded-xl border flex justify-between"},
        [
          React.createElement("div",{key:"l"},[
            React.createElement("div",{className:"font-semibold"},p.name),
            React.createElement("div",{className:"text-sm text-gray-600"},`${p.hostOrFile}:${p.port||""}/${p.database} (${p.username})`)
          ]),
          React.createElement("div",{key:"r",className:"flex gap-2"},[
            React.createElement("button",{onClick:()=>onEdit(p),className:"btn"},"⚙️"),
            React.createElement("button",{onClick:()=>onDelete(p),className:"btn text-red-600"},"🗑️"),
            React.createElement("button",{onClick:()=>onConnect(p),className:"btn-primary"},"Conectar")
          ])
        ]
      )
    )
  );
}
</script>
EOF

echo "==> git add/commit ..."
git add "$WEB_HTML"
git commit -m "feat(frontend): adiciona CRUD de perfis (editar/apagar/conectar) na UI"

echo
echo "Pronto! 🌟"
echo "- Branch atual: $BRANCH"
echo "- index.html atualizado com EditProfileModal, ProfilesList e helpers API"
echo "Agora rode ./run-dev.sh up e teste os botões."
