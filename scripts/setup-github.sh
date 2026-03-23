#!/bin/bash
# ============================================================
# AutoNomX — GitHub Project Setup Script
# ============================================================
# Usage:
#   chmod +x scripts/setup-github.sh
#   ./scripts/setup-github.sh
#
# Prerequisites:
#   - GitHub CLI (gh) installed and authenticated
#   - Run from the root of the autonomx repo
# ============================================================

set -e

echo "🚀 AutoNomX GitHub Setup"
echo "========================"
echo ""

# Check gh CLI
if ! command -v gh &> /dev/null; then
    echo "❌ GitHub CLI (gh) is not installed."
    echo "   Install: https://cli.github.com/"
    echo "   brew install gh  (macOS)"
    echo "   winget install GitHub.cli  (Windows)"
    exit 1
fi

# Check auth
if ! gh auth status &> /dev/null 2>&1; then
    echo "❌ GitHub CLI is not authenticated."
    echo "   Run: gh auth login"
    exit 1
fi

REPO=$(gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null)
if [ -z "$REPO" ]; then
    echo "❌ Not in a GitHub repository. Run this from the autonomx repo root."
    exit 1
fi

echo "📦 Repository: $REPO"
echo ""

# ============================================================
# LABELS
# ============================================================
echo "🏷️  Creating labels..."

create_label() {
    gh label create "$1" --color "$2" --description "$3" --force 2>/dev/null || true
}

# Layer labels
create_label "layer:dotnet"    "1565C0" ".NET Core katmanı"
create_label "layer:python"    "2E7D32" "Python katmanı"
create_label "layer:proto"     "F9A825" "gRPC proto dosyaları"
create_label "layer:infra"     "795548" "Docker, CI/CD, config"
create_label "layer:shared"    "7B1FA2" "Her iki katmanı etkiler"

# Type labels
create_label "type:feature"    "0E8A16" "Yeni özellik"
create_label "type:task"       "1D76DB" "Teknik görev"
create_label "type:bug"        "D93F0B" "Hata düzeltme"
create_label "type:docs"       "0075CA" "Dokümantasyon"
create_label "type:refactor"   "E4E669" "Kod iyileştirme"

# Agent labels
create_label "agent:po"        "FBCA04" "Product Owner agent"
create_label "agent:planner"   "C2E0C6" "Planner agent"
create_label "agent:architect" "BFD4F2" "Architect agent"
create_label "agent:coder"     "D4C5F9" "Coder/Worker agent"
create_label "agent:tester"    "F9D0C4" "Tester agent"
create_label "agent:reviewer"  "FEF2C0" "Reviewer agent"
create_label "agent:model-mgr" "E6B8AF" "Model Manager agent"

# Priority labels
create_label "priority:critical" "B60205" "Kritik"
create_label "priority:high"     "FF9800" "Yüksek"
create_label "priority:medium"   "FFEB3B" "Orta"
create_label "priority:low"      "8BC34A" "Düşük"

echo "✅ Labels created"
echo ""

# ============================================================
# MILESTONES
# ============================================================
echo "🎯 Creating milestones..."

create_milestone() {
    gh api repos/$REPO/milestones \
        --method POST \
        -f title="$1" \
        -f description="$2" \
        -f state="open" \
        2>/dev/null || echo "   ⚠️  Milestone '$1' may already exist"
}

create_milestone "M0: Project Setup & Foundation" \
    "Repo yapısı, .gitignore, README, proto dosyaları, docker-compose, temel konfigürasyon"

create_milestone "M1: .NET Core Domain & Infrastructure" \
    "Entity'ler, enum'lar, interface'ler, EF Core, Postgres bağlantısı, Repository pattern"

create_milestone "M2: Python Agent Core" \
    "BaseAgent, Registry, LiteLLM gateway, gRPC server, temel agent'lar"

create_milestone "M3: gRPC Integration & Event Bus" \
    ".NET ↔ Python bağlantısı, Postgres NOTIFY, uçtan uca mesajlaşma"

create_milestone "M4: Orchestrator & State Machine" \
    "Pipeline state machine, task board, iteratif döngü mantığı"

create_milestone "M5: CLI & First E2E Pipeline" \
    "CLI komutları, ilk uçtan uca çalışma, 'blog sitesi yap' senaryosu"

create_milestone "M6: Worker Pool & Parallel Execution" \
    "Multi-coder, self-pick, file locking, paralel task çalıştırma"

create_milestone "M7: Git Integration & PO Chat" \
    "Branch/merge workflow, PO interaktif chat, değişiklik yönetimi"

create_milestone "M8: Model Manager & Optimization" \
    "Model atama, performans izleme, otomatik model değiştirme"

create_milestone "M9: Polish & Documentation" \
    "Hata düzeltme, dokümantasyon, kurulum rehberi"

echo "✅ Milestones created"
echo ""

# ============================================================
# HELPER: Get milestone number by title prefix
# ============================================================
get_milestone_number() {
    gh api repos/$REPO/milestones --jq ".[] | select(.title | startswith(\"$1\")) | .number" 2>/dev/null
}

# Wait a moment for API
sleep 1

M0=$(get_milestone_number "M0")
M1=$(get_milestone_number "M1")
M2=$(get_milestone_number "M2")
M3=$(get_milestone_number "M3")
M4=$(get_milestone_number "M4")
M5=$(get_milestone_number "M5")
M6=$(get_milestone_number "M6")
M7=$(get_milestone_number "M7")
M8=$(get_milestone_number "M8")
M9=$(get_milestone_number "M9")

echo "📋 Milestone IDs: M0=$M0, M1=$M1, M2=$M2, M3=$M3, M4=$M4, M5=$M5, M6=$M6, M7=$M7, M8=$M8, M9=$M9"
echo ""

# ============================================================
# ISSUES
# ============================================================
echo "📝 Creating issues..."

create_issue() {
    local title="$1"
    local body="$2"
    local labels="$3"
    local milestone="$4"

    if [ -n "$milestone" ]; then
        gh issue create --title "$title" --body "$body" --label "$labels" --milestone "$milestone" 2>/dev/null
    else
        gh issue create --title "$title" --body "$body" --label "$labels" 2>/dev/null
    fi
    sleep 0.5
}

# ---- M0: Project Setup ----
echo "  📁 M0 issues..."

create_issue "[M0] Monorepo klasör yapısını oluştur" \
"## Açıklama
Tüm klasör yapısını oluştur (boş .gitkeep dosyalarıyla):

\`\`\`
autonomx/
├── protos/
├── src/
│   ├── core/AutoNomX.Domain/
│   ├── core/AutoNomX.Application/
│   ├── core/AutoNomX.Infrastructure/
│   ├── api/AutoNomX.Api/
│   ├── cli/AutoNomX.Cli/
│   └── tests/
├── agents/autonomx_agents/
│   ├── core/
│   ├── llm/prompts/
│   ├── agents/
│   ├── tools/
│   ├── executor/
│   └── grpc_services/
├── agents/plugins/
├── agents/tests/
├── docker/
├── scripts/
├── config/
├── workspace/
└── docs/
\`\`\`

## Kabul Kriterleri
- [ ] Tüm klasörler oluşturuldu
- [ ] .gitkeep dosyaları eklendi
- [ ] .gitignore güncellendi (bin/, obj/, __pycache__, .env, workspace/*)
" \
"type:task,layer:shared,priority:critical" \
"$M0"

create_issue "[M0] docker-compose.yml oluştur" \
"## Açıklama
Development ortamı için docker-compose.yml:

### Servisler:
- **postgres**: PostgreSQL 16, port 5432
- **autonomx-api**: .NET Core API (build from Dockerfile.api)
- **autonomx-agents**: Python agent runtime (build from Dockerfile.agents)

### İlk etapta sadece postgres servisi yeterli.

## Kabul Kriterleri
- [ ] docker-compose.yml oluşturuldu
- [ ] \`docker-compose up postgres\` ile Postgres çalışıyor
- [ ] .env.example dosyası oluşturuldu (connection string, vb.)
" \
"type:task,layer:infra,priority:high" \
"$M0"

create_issue "[M0] gRPC proto dosyalarını yaz" \
"## Açıklama
protos/ dizininde 4 proto dosyası:

### common.proto
- TaskStatus, PipelineState, AgentType enum'ları
- AgentMessage, TaskInfo, ProjectInfo mesajları

### agent_service.proto
- RunAgent (unary): Agent'ı çalıştır
- RunAgentStream (server streaming): Canlı çıktı ile çalıştır
- GetAgentStatus: Agent durumunu sorgula

### executor_service.proto
- ExecuteCode: Kod çalıştır (container'da)
- GetExecutionResult: Sonuç al

### llm_service.proto
- Complete: LLM completion
- StreamComplete: Streaming completion

## Kabul Kriterleri
- [ ] 4 proto dosyası yazıldı
- [ ] Proto dosyaları derlenebiliyor
- [ ] generate-protos.sh scripti C# ve Python kodu üretiyor
" \
"type:task,layer:proto,priority:critical" \
"$M0"

create_issue "[M0] .NET Solution ve projeler oluştur" \
"## Açıklama
Clean Architecture ile .NET 8 solution:

\`\`\`bash
dotnet new sln -n AutoNomX -o src/
dotnet new classlib -n AutoNomX.Domain -o src/core/AutoNomX.Domain
dotnet new classlib -n AutoNomX.Application -o src/core/AutoNomX.Application
dotnet new classlib -n AutoNomX.Infrastructure -o src/core/AutoNomX.Infrastructure
dotnet new webapi -n AutoNomX.Api -o src/api/AutoNomX.Api
dotnet new console -n AutoNomX.Cli -o src/cli/AutoNomX.Cli
dotnet new xunit -n AutoNomX.Domain.Tests -o src/tests/AutoNomX.Domain.Tests
dotnet new xunit -n AutoNomX.Application.Tests -o src/tests/AutoNomX.Application.Tests
dotnet new xunit -n AutoNomX.Integration.Tests -o src/tests/AutoNomX.Integration.Tests
\`\`\`

### Referanslar:
- Domain → (hiçbir referans yok)
- Application → Domain
- Infrastructure → Domain, Application
- Api → Application, Infrastructure
- Cli → Application, Infrastructure

### NuGet Paketleri (initial):
- Domain: -
- Application: MediatR
- Infrastructure: Npgsql.EntityFrameworkCore.PostgreSQL, Grpc.Net.Client
- Api: Grpc.AspNetCore
- Cli: System.CommandLine

## Kabul Kriterleri
- [ ] Solution build oluyor
- [ ] Proje referansları doğru
- [ ] NuGet paketleri yüklü
" \
"type:task,layer:dotnet,priority:critical" \
"$M0"

create_issue "[M0] Python proje yapısını oluştur" \
"## Açıklama
Python projesi (pyproject.toml / Poetry veya uv):

### Bağımlılıklar:
- grpcio, grpcio-tools (gRPC)
- litellm (LLM gateway)
- pydantic (model validation)
- asyncio
- docker (Docker SDK)
- pyyaml (config)
- pytest, pytest-asyncio (test)

### Yapı:
\`\`\`
agents/
├── pyproject.toml
├── autonomx_agents/
│   ├── __init__.py
│   ├── server.py
│   ├── core/__init__.py
│   ├── llm/__init__.py
│   ├── agents/__init__.py
│   ├── tools/__init__.py
│   ├── executor/__init__.py
│   └── grpc_services/__init__.py
├── plugins/README.md
└── tests/__init__.py
\`\`\`

## Kabul Kriterleri
- [ ] pyproject.toml oluşturuldu
- [ ] Bağımlılıklar tanımlandı
- [ ] \`pip install -e .\` çalışıyor
- [ ] pytest çalışıyor (boş test)
" \
"type:task,layer:python,priority:critical" \
"$M0"

create_issue "[M0] Makefile ve yardımcı scriptler" \
"## Açıklama
Geliştirme kolaylığı için Makefile + scriptler:

### Makefile targets:
- \`make setup\` → İlk kurulum (pip install, dotnet restore, docker up)
- \`make proto\` → Proto dosyalarından C# + Python kodu üret
- \`make build\` → .NET build
- \`make test\` → Tüm testleri çalıştır
- \`make run\` → docker-compose up
- \`make clean\` → Temizlik

### Scripts:
- \`scripts/generate-protos.sh\` → protoc ile kod üretimi
- \`scripts/setup.sh\` → İlk kurulum rehberi
- \`scripts/seed-db.sh\` → Varsayılan agent'ları DB'ye ekle

## Kabul Kriterleri
- [ ] Makefile çalışıyor
- [ ] Proto generation scripti çalışıyor
- [ ] README'de kurulum adımları var
" \
"type:task,layer:shared,priority:high" \
"$M0"

create_issue "[M0] Config dosyaları (YAML)" \
"## Açıklama
config/ dizininde 3 YAML dosyası:

### default-agents.yaml
Tüm agent tanımları (PO, Planner, Architect, Model Manager, Tester, Reviewer)
Her agent: name, type, default_model, temperature, tools, system_prompt_file

### default-pipelines.yaml
Standard pipeline tanımı (adımlar, döngü kuralları, max retry)

### llm-models.yaml
Kullanılabilir model listesi (Ollama, LM Studio, API modelleri)
Her model: name, provider, base_url, context_window, strengths, cost

## Kabul Kriterleri
- [ ] 3 YAML dosyası oluşturuldu
- [ ] Agent tanımları eksiksiz
- [ ] Model listesi mantıklı
" \
"type:task,layer:shared,priority:medium" \
"$M0"

create_issue "[M0] README.md güncelle" \
"## Açıklama
Kapsamlı README.md:

- Proje açıklaması (ne, neden, nasıl)
- Mimari diyagram (ASCII art)
- Ön gereksinimler (.NET 8, Python 3.11, Docker, PostgreSQL)
- Hızlı başlangıç (3 adımda çalıştır)
- CLI kullanım örnekleri
- Geliştirme rehberi
- Katkıda bulunma
- Lisans

## Kabul Kriterleri
- [ ] README bilgilendirici ve eksiksiz
- [ ] Kurulum adımları test edildi
" \
"type:docs,layer:shared,priority:medium" \
"$M0"


# ---- M1: .NET Domain ----
echo "  🔵 M1 issues..."

create_issue "[M1] Domain Entity'leri oluştur" \
"## Açıklama
AutoNomX.Domain/Entities/ altında:

- Project.cs (Id, Name, Description, Status, Config, CreatedAt)
- AgentTask.cs (Id, ProjectId, Title, Status, AssignedAgent, Dependencies, FilesTouched)
- Agent.cs (Id, Name, Type, LlmConfig, SystemPrompt, Tools)
- PipelineRun.cs (Id, ProjectId, Status, CurrentStep, StartedAt, CompletedAt)
- AgentMessage.cs (Id, ProjectId, FromAgent, ToAgent, EventType, Payload, Timestamp)
- CoderWorker.cs (Id, Model, Provider, Status, CurrentTaskId, Metrics)
- ChangeLogEntry.cs (Id, ProjectId, ChangeType, UserMessage, Decisions, Timestamp)

## Kabul Kriterleri
- [ ] Tüm entity'ler oluşturuldu
- [ ] Navigation property'ler tanımlı
- [ ] Nullable reference types enabled
" \
"type:feature,layer:dotnet,priority:critical" \
"$M1"

create_issue "[M1] Domain Enum'lar ve Interface'ler" \
"## Açıklama
### Enums/:
- TaskStatus (Pending, Ready, Locked, InProgress, Testing, Reviewing, Done, Failed)
- PipelineState (Planning, Architecting, Coding, Testing, Reviewing, Completed, Paused, Failed)
- AgentType (ProductOwner, Planner, Architect, ModelManager, Coder, Tester, Reviewer, DevOps)
- WorkerStatus (Idle, Working, Paused, Terminated)

### Interfaces/:
- IProjectRepository, IAgentRepository, ITaskRepository, IWorkerRepository
- IEventBus (Publish, Subscribe, Listen)
- IAgentGateway (gRPC client soyutlaması)
- ICodeExecutor (ExecuteAsync, GetResultAsync)
- IPipelineRunRepository

### Events/:
- TaskCompletedEvent, CodeReadyEvent, TestResultEvent
- ReviewResultEvent, WorkerIdleEvent, PipelineStateChangedEvent
" \
"type:feature,layer:dotnet,priority:critical" \
"$M1"

create_issue "[M1] EF Core DbContext ve Migration'lar" \
"## Açıklama
AutoNomX.Infrastructure/Persistence/:

- AppDbContext.cs (tüm DbSet'ler, JSONB konfigürasyonu)
- Entity konfigürasyonları (Fluent API)
- İlk migration (InitialCreate)
- Connection string: appsettings.json'dan

JSONB alanlar: Project.Config, Agent.LlmConfig, AgentMessage.Payload, TaskBoard.FilesTouched

## Kabul Kriterleri
- [ ] DbContext oluşturuldu
- [ ] Migration çalışıyor
- [ ] Postgres'e bağlanıp tablo oluşturuyor
" \
"type:feature,layer:dotnet,priority:critical" \
"$M1"

create_issue "[M1] Repository implementasyonları" \
"## Açıklama
AutoNomX.Infrastructure/Persistence/Repositories/:

- ProjectRepository : IProjectRepository
- AgentRepository : IAgentRepository
- TaskRepository : ITaskRepository (task board sorgulari dahil)
- WorkerRepository : IWorkerRepository
- PipelineRunRepository : IPipelineRunRepository

Temel CRUD + özel sorgular (GetReadyTasks, GetWorkersByStatus, vb.)

## Kabul Kriterleri
- [ ] Tüm repository'ler implement edildi
- [ ] Unit testler yazıldı
" \
"type:feature,layer:dotnet,priority:high" \
"$M1"

create_issue "[M1] PostgreSQL EventBus (LISTEN/NOTIFY)" \
"## Açıklama
AutoNomX.Infrastructure/EventBus/:

- PostgresEventBus : IEventBus
  - Publish(channel, message) → NOTIFY
  - Subscribe(channel, handler) → LISTEN
  - Background listener (Npgsql async notification)
- InMemoryEventBus : IEventBus (test için)

Channels: task_events, pipeline_events, worker_events

## Kabul Kriterleri
- [ ] NOTIFY gönderilebiliyor
- [ ] LISTEN ile mesaj alınabiliyor
- [ ] Integration test var
" \
"type:feature,layer:dotnet,priority:high" \
"$M1"


# ---- M2: Python Agent Core ----
echo "  🟢 M2 issues..."

create_issue "[M2] BaseAgent ve Agent Registry" \
"## Açıklama
agents/autonomx_agents/core/:

### base_agent.py
- Abstract BaseAgent class
- execute(context) → AgentResult (abstract method)
- render_prompt(template, **kwargs) → formatted prompt
- llm property (LLM Gateway access)

### agent_registry.py
- @agent_register(name) decorator
- AgentRegistry.get(name) → agent class
- AgentRegistry.list() → all registered agents
- Auto-discovery from agents/ dir + plugins/ dir

### agent_config.py
- AgentConfig dataclass (name, type, model, temperature, tools, system_prompt)
- Load from YAML

### context.py
- AgentContext (project_id, task, files, history, feedback)

## Kabul Kriterleri
- [ ] BaseAgent abstract class çalışıyor
- [ ] Registry agent'ları keşfediyor
- [ ] Plugin dizininden custom agent yüklenebiliyor
- [ ] Pytest testleri var
" \
"type:feature,layer:python,priority:critical" \
"$M2"

create_issue "[M2] LLM Gateway (LiteLLM wrapper)" \
"## Açıklama
agents/autonomx_agents/llm/:

### gateway.py
- LLMGateway class
- complete(model, messages, **kwargs) → response
- stream_complete(model, messages, **kwargs) → async generator
- Model routing: ollama/, lm_studio/, openai/, anthropic/ prefix'leri
- Token counting and logging
- Error handling + retry logic

### models.py
- ModelInfo dataclass (name, provider, context_window, strengths)
- Load available models from llm-models.yaml
- Health check (model erişilebilir mi?)

## Kabul Kriterleri
- [ ] Ollama modeline completion çağrısı yapılabiliyor
- [ ] Streaming çalışıyor
- [ ] Model listesi YAML'dan yüklenebiliyor
- [ ] Testler var (mock LLM ile)
" \
"type:feature,layer:python,priority:critical" \
"$M2"

create_issue "[M2] gRPC Server (Python tarafı)" \
"## Açıklama
agents/autonomx_agents/:

### server.py
- gRPC server entry point
- Reflection enabled (debug için)
- Graceful shutdown

### grpc_services/agent_servicer.py
- AgentServiceServicer
- RunAgent: Agent'ı çalıştır, sonucu döndür
- RunAgentStream: Canlı çıktı stream'i
- GetAgentStatus: Agent durumunu döndür

Proto'dan generate edilen Python kodunu kullanır.

## Kabul Kriterleri
- [ ] gRPC server başlatılabiliyor
- [ ] .NET tarafından çağrılabiliyor (basit test)
- [ ] Streaming çalışıyor
" \
"type:feature,layer:python,priority:critical" \
"$M2"

create_issue "[M2] Temel agent implementasyonları (Planner, Coder, Tester)" \
"## Açıklama
agents/autonomx_agents/agents/:

İlk 3 temel agent:
- planner_agent.py (@agent_register('planner'))
- coder_agent.py (@agent_register('coder'))
- tester_agent.py (@agent_register('tester'))

Her agent:
- BaseAgent'ı extend eder
- System prompt'u prompts/ dizininden okur
- JSON output format döndürür
- Tools listesi tanımlı

Prompts:
- agents/autonomx_agents/llm/prompts/planner.md
- agents/autonomx_agents/llm/prompts/coder.md
- agents/autonomx_agents/llm/prompts/tester.md

## Kabul Kriterleri
- [ ] 3 agent implement edildi
- [ ] Promptlar detaylı ve çalışır
- [ ] Registry'de görünüyorlar
- [ ] Mock LLM ile test edildi
" \
"type:feature,layer:python,agent:planner,agent:coder,agent:tester,priority:high" \
"$M2"


# ---- M3-M9 Summary Issues ----
echo "  📋 M3-M9 summary issues..."

create_issue "[M3] .NET gRPC Client + Python Server entegrasyonu" \
"## Açıklama
- .NET: AgentGrpcClient : IAgentGateway implementasyonu
- Python: gRPC server agent_servicer tamamlama
- Uçtan uca test: .NET → gRPC → Python → LLM → gRPC → .NET
- Postgres NOTIFY entegrasyonu (Python → NOTIFY → .NET LISTEN)

Bu milestone'da iki katman birbirine bağlanır.
" \
"type:feature,layer:shared,priority:critical" \
"$M3"

create_issue "[M4] Orchestrator State Machine" \
"## Açıklama
- PipelineStateMachine (Stateless kütüphanesi)
- State transitions: Planning → Architecting → Coding → Testing → Reviewing → loop
- Task board yönetimi
- Iteratif döngü mantığı
- Yarıda kalan işleri devam ettirme (state persistence)

Bu milestone'da pipeline çalışmaya başlar.
" \
"type:feature,layer:dotnet,priority:critical" \
"$M4"

create_issue "[M5] CLI Uygulaması ve İlk E2E Pipeline" \
"## Açıklama
- CLI komutları: new, run, status, chat, workers, projects, logs
- İlk uçtan uca senaryo: \`autonomx new 'Blog API'\` → çalışan kod çıktısı
- System.CommandLine ile zengin CLI deneyimi
- Progress bar, tablo formatı, renkli çıktı

Bu milestone'da sistem ilk kez uçtan uca çalışır.
" \
"type:feature,layer:dotnet,priority:critical" \
"$M5"

create_issue "[M6] Worker Pool ve Paralel Çalışma" \
"## Açıklama
- Dinamik worker pool (config'den template, runtime'da instance)
- Task board'dan self-pick mantığı
- Hard file locking
- Paralel task çalıştırma
- Worker ekleme/çıkarma (çalışırken)
" \
"type:feature,layer:shared,priority:high" \
"$M6"

create_issue "[M7] Git Entegrasyonu ve PO Chat" \
"## Açıklama
- Feature branch per task workflow
- Auto commit, merge, conflict detection
- Product Owner interaktif chat modu
- Değişiklik yönetimi (add/modify/remove story)
- Change log
" \
"type:feature,layer:shared,priority:high" \
"$M7"

create_issue "[M8] Model Manager Agent" \
"## Açıklama
- Model Manager agent implementasyonu
- Task bazlı model/worker atama
- Başarısızlıkta otomatik model yükseltme
- Performans metrikleri ve raporlama
- Eskalasyon merdiveni
" \
"type:feature,agent:model-mgr,priority:medium" \
"$M8"

create_issue "[M9] Dokümantasyon ve Polish" \
"## Açıklama
- API dokümantasyonu
- Kurulum rehberi (step-by-step)
- Plugin geliştirme rehberi
- Mimari doküman güncellemesi
- Hata düzeltmeleri
- Performance optimizasyonu
" \
"type:docs,priority:medium" \
"$M9"


echo ""
echo "============================================================"
echo "✅ GitHub setup complete!"
echo ""
echo "📊 Summary:"
echo "   - Labels: ~20 created"
echo "   - Milestones: 10 created (M0-M9)"
echo "   - Issues: ~20 created"
echo ""
echo "🔗 View your project: https://github.com/$REPO"
echo "   Issues:     https://github.com/$REPO/issues"
echo "   Milestones: https://github.com/$REPO/milestones"
echo ""
echo "📝 Next steps:"
echo "   1. Review issues and milestones on GitHub"
echo "   2. Start with M0 issues"
echo "   3. Use Claude Code: claude"
echo "   4. Tell Claude: 'Implement M0 starting with issue #1'"
echo "============================================================"
