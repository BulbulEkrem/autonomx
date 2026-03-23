# AutoNomX

> Autonomous Software Company — AI-powered development pipeline

AutoNomX, doğal dil isteklerini otomatik olarak analiz eden, planlayan, kodlayan, test eden ve teslim eden otonom bir yazılım geliştirme sistemidir.

## Mimari

```
┌──────────────────────────────────┐     ┌──────────────────────────────────┐
│       .NET Core 8+ (Kontrol)     │     │     Python 3.11+ (Agent Runtime) │
│                                  │     │                                  │
│  API (ASP.NET) · CLI · Orchestr. │◄───►│  Agents · LLM Gateway (LiteLLM) │
│  State Machine · Project Manager │gRPC │  Tools · Code Executor (Docker)  │
│         PostgreSQL 16+           │     │  Plugin System                   │
└──────────────────────────────────┘     └──────────────────────────────────┘
```

**İki katmanlı mimari:** .NET Core kontrol düzlemi + Python agent runtime, gRPC ve PostgreSQL LISTEN/NOTIFY üzerinden haberleşir.

## Agent Ekibi

| Agent | Rol | Model |
|-------|-----|-------|
| Product Owner | İstek analizi, user story, chat | deepseek-r1:14b |
| Planner | Story → teknik task | deepseek-r1:14b |
| Architect | Yapı, sprint yönetimi | qwen2.5-coder:14b |
| Model Manager | LLM atama, optimizasyon | deepseek-r1:14b |
| Coder Workers | Kod yazma (dinamik pool) | Değişken |
| Tester | Test yazma + çalıştırma | qwen2.5-coder:14b |
| Reviewer | Kod kalitesi, onay | claude-sonnet |

## Hızlı Başlangıç

### Gereksinimler

- .NET 8 SDK
- Python 3.11+
- Docker & Docker Compose
- Git

### Kurulum

```bash
# Repo'yu klonla
git clone https://github.com/BulbulEkrem/autonomx.git
cd autonomx

# İlk kurulum
make setup

# Altyapıyı başlat (PostgreSQL + Ollama)
make run-dev

# .NET projesini derle
make build

# Testleri çalıştır
make test
```

### Komutlar

```bash
make help        # Tüm komutları göster
make setup       # İlk kurulum
make proto       # Proto dosyalarından kod üret
make build       # .NET solution derle
make test        # Tüm testleri çalıştır
make run         # Tüm servisleri başlat
make run-dev     # Sadece altyapı (postgres + ollama)
make stop        # Servisleri durdur
make logs        # Logları izle
make lint        # Linter çalıştır
make clean       # Build artifact'larını temizle
```

## Proje Yapısı

```
autonomx/
├── protos/                  # gRPC proto tanımları (paylaşılan)
├── src/                     # .NET Core (Clean Architecture)
│   ├── AutoNomX.sln
│   ├── core/
│   │   ├── AutoNomX.Domain/          # Entity, Enum, Interface
│   │   ├── AutoNomX.Application/     # CQRS (MediatR)
│   │   └── AutoNomX.Infrastructure/  # DB, gRPC, Docker, EventBus
│   ├── api/AutoNomX.Api/             # Web API + SignalR
│   ├── cli/AutoNomX.Cli/             # CLI (System.CommandLine)
│   └── tests/
├── agents/                  # Python agent runtime
│   ├── autonomx_agents/
│   │   ├── core/            # BaseAgent, Registry, Config
│   │   ├── llm/             # LiteLLM gateway
│   │   ├── agents/          # Built-in agent'lar
│   │   ├── tools/           # Agent araçları
│   │   ├── executor/        # Kod çalıştırma (Docker/Host)
│   │   └── grpc_services/   # gRPC servis implementasyonları
│   ├── plugins/             # Kullanıcı plugin'leri
│   └── tests/
├── docker/                  # Dockerfile'lar
├── config/                  # YAML konfigürasyonlar
├── scripts/                 # Yardımcı scriptler
├── workspace/               # Runtime proje workspace
└── docs/                    # Dokümantasyon
```

## Pipeline

```
Kullanıcı İsteği → Product Owner → Planner → Architect ──┐
                                                          │
    ┌─────────── İTERATİF DÖNGÜ ─────────────────────────┤
    │  Architect → Model Manager → Workers → Tester       │
    │  → Reviewer → ONAY → merge → döngü başına ──────────┘
    │            → REVİZYON → worker'a geri dön           │
    └──── Tüm task'lar bitince → TESLİM ─────────────────┘
```

## Dokümantasyon

- [Mimari Tasarım](docs/ARCHITECTURE.md)
- [Hızlı Başlangıç](docs/QUICKSTART.md)

## Lisans

Bu proje MIT lisansı altında lisanslanmıştır.
