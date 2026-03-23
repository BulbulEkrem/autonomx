# AutoNomX — Mimari Tasarım Dokümanı

> **Versiyon:** 1.0  
> **Tarih:** 23 Mart 2026  
> **Durum:** Taslak — Mimari Kararlar Tamamlandı

---

## 1. Yönetici Özeti

AutoNomX, yapay zeka destekli otonom bir yazılım geliştirme sistemidir. Kullanıcıdan alınan doğal dil isteklerini otomatik olarak analiz eder, planlar, kodlar, test eder ve teslim eder. Sistem, birbirinden bağımsız AI agent'lardan oluşan bir ekip gibi çalışır.

### 1.1 Temel Özellikler
- Tam otonom yazılım geliştirme pipeline'ı (planlama → kodlama → test → teslim)
- Dinamik worker pool ile paralel kod geliştirme
- Local LLM önceliği (Ollama, LM Studio) + dış API desteği
- Her agent farklı LLM modeli kullanabilir
- Model Manager ile otomatik model optimizasyonu
- Git-first pipeline (her task bir branch)
- Product Owner ile interaktif sohbet (yön değiştirme)
- Çoklu proje yönetimi
- Genişletilebilir input adapterleri (CLI, Web, Telegram, vb.)
- Plugin sistemi ile yeni agent ekleme

---

## 2. Teknoloji Seçimleri

| Katman | Teknoloji | Neden |
|--------|-----------|-------|
| Ana Kontrol & API | .NET Core 8+ (ASP.NET Core) | Geliştirici uzmanlık alanı, güçlü tip sistemi, gRPC desteği |
| Agent Runtime | Python 3.11+ | AI ekosistemi (LiteLLM, vb.), hızlı prototipleme |
| LLM Gateway | LiteLLM | Tüm LLM sağlayıcılara tek API |
| Veritabanı | PostgreSQL 16+ | JSONB, LISTEN/NOTIFY, ölçeklenebilirlik |
| Inter-Service | gRPC + Protobuf | Hızlı, tip güvenli, streaming |
| Event Bus | PostgreSQL LISTEN/NOTIFY | Ek altyapı gerektirmez, Redis'e geçiş kolay |
| Kod Çalıştırma | Docker (Abstract/Değiştirilebilir) | Güvenli izolasyon, Strategy Pattern |
| CLI Framework | .NET System.CommandLine | Doğal .NET entegrasyonu |
| ORM | EF Core / Dapper | Repository Pattern ile soyutlanmış |
| Versiyon Kontrol | Git (LibGit2Sharp / CLI) | Her proje kendi repo'su |
| Web UI (Faz 2) | React / Next.js | API üzerinden iletişim |
| Realtime (Faz 2) | SignalR | Pipeline canlı güncellemeleri |

---

## 3. Mimari Tasarım

### 3.1 Katmanlı Mimari

Sistem iki ana katmandan oluşur: .NET Core kontrol katmanı ve Python agent runtime katmanı. Bu iki katman gRPC üzerinden haberleşir, event'ler Postgres NOTIFY ile iletilir.

```
┌──────────────────────────────────────────────────┐
│                  .NET Core Katmanı                │
│                                                   │
│  ┌─────────┐  ┌──────────────┐  ┌────────────┐  │
│  │ CLI App │  │ ASP.NET API  │  │ Telegram   │  │
│  └────┬────┘  └──────┬───────┘  │ Bot (later)│  │
│       │              │          └─────┬──────┘  │
│       └──────┬───────┘                │         │
│              │                        │         │
│       ┌──────▼────────────────────────▼──┐      │
│       │        Orchestrator Service       │      │
│       │     (State Machine + Scheduler)   │      │
│       └──────────────┬───────────────────┘      │
│                      │                           │
│       ┌──────────────▼───────────────────┐      │
│       │        Project Manager            │      │
│       │   (EF Core + Repository Pattern)  │      │
│       └──────────────┬───────────────────┘      │
│                      │                           │
│              ┌───────▼────────┐                  │
│              │  Postgres DB   │                  │
│              └───────┬────────┘                  │
└──────────────────────┼───────────────────────────┘
                       │
                 gRPC + Events
                       │
┌──────────────────────┼───────────────────────────┐
│              Python Agent Runtime                 │
│                      │                           │
│       ┌──────────────▼───────────────────┐      │
│       │       Agent Service (gRPC)        │      │
│       └──────┬───────┬───────┬───────────┘      │
│              │       │       │                   │
│       ┌──────▼┐ ┌────▼───┐ ┌▼────────┐         │
│       │Planner│ │ Coder  │ │ Tester  │ ...      │
│       └──────┬┘ └────┬───┘ └┬────────┘         │
│              └───────┼──────┘                    │
│                      │                           │
│              ┌───────▼────────┐                  │
│              │  LLM Gateway   │                  │
│              │   (LiteLLM)    │                  │
│              └───────┬────────┘                  │
│                      │                           │
│          ┌───────┬───┴────┬─────────┐           │
│          │Ollama │LMStudio│Claude   │...        │
│          └───────┘────────┘─────────┘           │
└──────────────────────────────────────────────────┘
```

### 3.2 .NET ↔ Python İletişimi

| İletişim Türü | Kanal | Neden |
|---------------|-------|-------|
| .NET → Python (görev ver) | gRPC (unary + server streaming) | Direkt çağrı, tip güvenli, canlı çıktı |
| Python → .NET (sonuç bildir) | PostgreSQL NOTIFY | Basit, DB ile atomik |
| State değişiklikleri | PostgreSQL NOTIFY | Veri yaz + event gönder atomik |
| İleride büyürse | Redis Pub/Sub | NOTIFY limitlerini aşınca geçiş |

### 3.3 Kod Çalıştırma Ortamı (Strategy Pattern)

Kod çalıştırma ortamı soyutlanmıştır ve değiştirilebilir. Varsayılan: Docker.

| Strateji | Kullanım | Risk Seviyesi |
|----------|----------|---------------|
| DockerExecutor | Varsayılan — her kod izole container'da | Düşük |
| HostExecutor | Linting, git, format gibi güvenli işler | Yüksek |
| SandboxExecutor | İleride: e2b.dev, nsjail gibi | Düşük |

---

## 4. Agent Tasarımı

### 4.1 Agent Hiyerarşisi ve Rolleri

```
                    ┌──────────────┐
                    │   Product    │
                    │   Owner      │  ← İsteği alır, büyük resmi görür
                    └──────┬───────┘
                           │
                    ┌──────▼───────┐
                    │   Planner    │  ← Teknik plana çevirir
                    └──────┬───────┘
                           │
                    ┌──────▼───────┐
                    │  Architect   │  ← Dosya yapısı, sprint yönetimi
                    └──────┬───────┘
                           │
              ┌────────────┼────────────┐
              │            │            │
       ┌──────▼───┐ ┌─────▼────┐ ┌────▼──────┐
       │ Worker-A │ │ Worker-B │ │ Worker-C  │  ← Dinamik pool
       └──────┬───┘ └─────┬────┘ └────┬──────┘
              └────────────┼────────────┘
                           │
                    ┌──────▼───────┐
                    │   Tester     │
                    └──────┬───────┘
                           │
                    ┌──────▼───────┐
                    │  Reviewer    │
                    └──────────────┘
```

| Agent | Rol | Önerilen Model | Tetiklenme |
|-------|-----|----------------|------------|
| Product Owner | İstek analizi, user story, kullanıcı chat | deepseek-r1:14b | Proje başı + chat |
| Planner | Story → teknik task dönüşümü | deepseek-r1:14b | PO sonrası |
| Architect | Yapı, scaffolding, sprint yönetimi | qwen2.5-coder:14b | Her döngü başı |
| Model Manager | LLM atama, performans, model değiştirme | deepseek-r1:14b | Task ataması + başarısızlık |
| Coder Workers | Kod yazma (dinamik pool) | DEĞİŞKEN | Task board'dan iş alınca |
| Tester | Test yazma + çalıştırma | qwen2.5-coder:14b | Kod commit sonrası |
| Reviewer | Kod kalitesi, güvenlik, onay | claude-sonnet (API) | Test geçince |
| DevOps (Faz 2) | Docker, CI/CD, deployment | codellama:13b | Proje tamamlanınca |

### 4.2 Product Owner — İnteraktif Chat

Kullanıcı Product Owner ile istediği zaman sohbet edebilir. PO değişiklik isteklerini analiz eder, etki değerlendirmesi yapar ve kullanıcı onayı ile sistemi günceller.

**Değişiklik Türleri:**
- `ADD_STORY`: Yeni user story ekleme
- `MODIFY_STORY`: Mevcut story güncelleme
- `REMOVE_STORY`: Story iptal etme
- `CHANGE_PRIORITY`: Öncelik değiştirme
- `PAUSE_PROJECT`: Projeyi duraklat
- `CHANGE_SCOPE`: Kapsam değişikliği

**Scope Creep Koruması:** Kümülatif değişiklikler orijinal kapsamın %30'unu aşarsa PO uyarı verir ve faz bölme önerir.

### 4.3 Architect — Sprint Manager Rolü

Architect sadece proje başında değil, her döngüde aktif çalışır:
- Tamamlanan işlerin mevcut kodla entegrasyonunu kontrol eder
- Paralel branch'ler arası conflict tespiti yapar
- Sıradaki task(lar)ı seçer ve board'u günceller
- Mimaride güncelleme gerekiyorsa convention'ları düzeltir
- Yeni ortak bileşenler keşfederse gelecek task'lara bildirir

### 4.4 Model Manager — AI Operasyonları

Model Manager, diğer agent'ların hangi LLM modelini kullanacağına karar verir ve performansa göre otomatik optimizasyon yapar.

**Eskalasyon Merdiveni:**
1. Aynı worker, modeli bir kademe yükselt
2. Aynı worker, en iyi mevcut model
3. Farklı worker'a devret
4. Dış API modeli kullan (son çare)

Şimdilik: Kullanıcı karar verir. İleride: Model Manager otomatik karar verir.

---

## 5. Dinamik Worker Pool (Coder)

### 5.1 Konsept

Coder'lar sabit roller değil, dinamik worker pool olarak çalışır. Kullanıcı kalıp (template) tanımlar, sistem runtime'da instance'lar üretir. Her worker genel amaçlıdır ve task board'dan kendisi iş seçer.

**Örnek Konfigürasyon:**
```json
{
  "CoderPool": {
    "Workers": [
      { "Count": 2, "Model": "ollama/qwen2.5-coder:32b", "Provider": "ollama" },
      { "Count": 1, "Model": "lm_studio/deepseek-coder-v2:33b", "Provider": "lm_studio" }
    ]
  }
}
```

Bu konfigürasyon 3 worker oluşturur: worker-a (qwen:32b), worker-b (qwen:32b), worker-c (deepseek:33b).

### 5.2 Task Board (Kanban)

Tüm task'lar ortak bir board'da tutulur. Worker'lar boşaldıkça board'dan iş çeker.

```
┌─────────────────────────────────────────────────────────────┐
│                      TASK BOARD                              │
│                                                              │
│  READY            IN PROGRESS         TESTING      DONE     │
│  ──────           ───────────         ───────      ────     │
│  T-008 [M]        T-005 [L]          T-003 ✓      T-001 ✅ │
│  T-009 [S]          → worker-a       T-004 ✓      T-002 ✅ │
│  T-010 [L]        T-006 [M]                                │
│  T-011 [S]          → worker-b                              │
│                   T-007 [S]                                  │
│                     → worker-c                               │
└─────────────────────────────────────────────────────────────┘
```

### 5.3 Kendi İş Seçme Mantığı

Her worker boşaldığında kendi kararıyla iş seçer:
1. **Bağımlılıklar:** Sadece tüm bağımlılıkları tamamlanmış task'ları al
2. **Dosya çakışması:** Başka worker'ın düzenlediği dosyalara dokunan task'lardan kaçın
3. **Bağlam avantajı:** İlgili bir task'ı yeni bitirdiyse, aynı alandaki işi tercih et
4. **Karmaşıklık eşleşmesi:** Model gücüne uygun task seç
5. **Öncelik:** must > should > could sırasına uy

### 5.4 Paralel Çalışma ve Conflict Yönetimi

| Strateji | Açıklama | Kullanım |
|----------|----------|----------|
| Hard Lock (varsayılan) | Aynı dosyaya iki worker atanamaz | Conflict'ı önleme |
| Soft Lock | Uyarı verir ama engellemez | Deneyimli ekip |
| Git Merge | Conflict oluşursa Architect çözer | Kurtarma |

### 5.5 Worker Yaşam Döngüsü

```
IDLE → PICK TASK → WORKING (git branch) → COMMIT → HAND OFF (Tester)
  ↑                                                        │
  │         Test PASS → Reviewer → APPROVE → merge ────────┘
  │                                 REVISION → fix (max 3)
  └── back to IDLE ────────────────────────────────────────┘
```

---

## 6. Pipeline (İteratif Geliştirme Döngüsü)

### 6.1 Akış

Pipeline lineer değil, iteratiftir. Her tamamlanan task sonrası Architect'a dönülür, yeni işler atanır. Tüm task'lar bitene kadar döngü devam eder.

1. Kullanıcı istek verir
2. Product Owner analiz eder, user story oluşturur
3. Planner teknik task'lara böler
4. Architect yapıyı oluşturur, task board'u hazırlar
5. Model Manager task için en uygun worker + model seçer
6. Worker(lar) board'dan iş alır, git branch açar, kodlar, commit eder
7. Tester test yazar ve çalıştırır
8. Test FAIL → Worker düzeltir (max 3 deneme). Model Manager model değiştirebilir
9. Test PASS → Reviewer inceler
10. Reviewer APPROVE → git merge to main, Architect'a dön (adım 4)
11. Reviewer NEEDS_REVISION → Worker düzeltir (max 3 tur)
12. Tüm task'lar tamamlanınca → Proje teslim

### 6.2 Git-First Workflow

Her proje bir git reposudur. Her task için feature branch açılır.

| Kural | Değer |
|-------|-------|
| Branch isimlendirme | `feature/{task_id}-{short_description}` |
| Commit format | `{type}: {description}` (feat, fix, test, refactor, docs) |
| Merge koşulu | Testler geçmeli + Reviewer onayı |
| Test başarısızlığı stratejisi | fix_forward (revert değil, düzelt) — max 3 deneme |
| Review red stratejisi | Aynı branch'te yeni commit — max 3 tur |

---

## 7. Veritabanı Tasarımı (PostgreSQL)

### 7.1 Ana Tablolar

| Tablo | Amaç | Anahtar Alanlar |
|-------|------|-----------------|
| projects | Proje metadata ve konfigürasyonu | id, name, status, config (JSONB) |
| tasks | Teknik görevler | id, project_id, status, assigned_agent, dependencies |
| agents | Agent tanımları ve konfigürasyonları | id, name, type, llm_config (JSONB) |
| pipeline_runs | Pipeline çalıştırma geçmişi | id, project_id, status, current_step |
| coder_workers | Dinamik worker tanımları | id, model, provider, status, current_task_id |
| task_board | Kanban board (file locking dahil) | task_id, status, assigned_worker, files_touched, locked_files |
| agent_history | Her agent'ın konuşma geçmişi | agent_id, agent_instance_id, task_id, content, model_used |
| agent_metrics | Performans metrikleri | agent_id, model_used, avg_iterations, avg_scores |
| messages | Agent iletişim logu | from_agent, to_agent, event_type, payload (JSONB) |
| files | Proje dosya takibi | project_id, task_id, path, content_hash |
| change_log | PO değişiklik geçmişi | project_id, change_type, user_message, decisions |

### 7.2 Agent Geçmişi — Ayrı Depolama

Her agent'ın ve her coder worker instance'ının geçmişi ayrı ayrı saklanır:
- Her worker'ın performansı bağımsız izlenebilir
- Model Manager başarısızlık örüntülerini tespit edebilir
- Worker kendi önceki denemelerini context olarak alabilir
- Token kullanımı ve maliyet worker/model bazında raporlanabilir

---

## 8. LLM Gateway (LiteLLM)

Tüm LLM çağrıları LiteLLM üzerinden yapılır. Bu sayede Ollama, LM Studio, OpenAI, Anthropic, Groq, Mistral ve diğer sağlayıcılara tek API ile erişilir.

| Sağlayıcı | Format | Örnek |
|------------|--------|-------|
| Ollama (yerel) | `ollama/{model}` | `ollama/qwen2.5-coder:32b` |
| LM Studio (yerel) | `lm_studio/{model}` | `lm_studio/deepseek-coder-v2:33b` |
| OpenAI | `openai/{model}` | `openai/gpt-4o` |
| Anthropic | `anthropic/{model}` | `anthropic/claude-sonnet` |
| Groq | `groq/{model}` | `groq/llama3-70b` |
| Mistral | `mistral/{model}` | `mistral/mistral-large` |

Local modeller her zaman tercih edilir. API modelleri son çaredir (maliyet + gizlilik).

---

## 9. Geliştirme Fazları

### Faz 1 — Temel Sistem (MVP)
- .NET Core: Domain modeller, Orchestrator, Project Manager, CLI
- Python: BaseAgent, gRPC server, LiteLLM gateway
- gRPC proto tanımları ve iletişim
- PostgreSQL şema ve repository
- Temel pipeline (PO → Planner → Architect → Coder → Tester → Reviewer)
- Tek worker ile çalışma
- CLI ile proje oluşturma ve izleme
- Git entegrasyonu (branch, commit, merge)

### Faz 2 — Paralel Çalışma
- Dinamik worker pool (multi-coder)
- Task board ve file locking
- Worker'ın kendi iş seçme mantığı
- Model Manager agent'ı
- PO interaktif chat
- Agent geçmişi ve performans metrikleri

### Faz 3 — Arayüzler ve Entegrasyonlar
- Web UI (React/Next.js + SignalR)
- Telegram bot entegrasyonu
- DevOps agent'ı (Docker, CI/CD)
- Plugin marketplace
- Model Manager otomatik karar verme
- Worker otomatik ölçekleme

---

## 10. CLI Komut Referansı

| Komut | Açıklama |
|-------|----------|
| `autonomx new "Açıklama"` | Yeni proje oluştur, pipeline başlat |
| `autonomx run --project {id}` | Mevcut projenin pipeline'ını başlat/devam ettir |
| `autonomx status {id}` | Proje durumu ve task detayları |
| `autonomx chat {id}` | PO ile interaktif sohbet |
| `autonomx workers` | Worker havuzu durumu |
| `autonomx worker add --model {model}` | Çalışırken worker ekle |
| `autonomx worker remove {id}` | Çalışırken worker çıkar |
| `autonomx config coders --count N --model M` | Worker şablonu tanımla |
| `autonomx agent add {type} --model {m}` | Yeni agent ekle (plugin) |
| `autonomx projects` | Tüm projeleri listele |
| `autonomx logs {project_id} --agent {name}` | Agent loglarını gör |

---

## 11. Mimari Kararlar Özeti

| Karar | Seçim | Gerekçe |
|-------|-------|---------|
| Framework | Tamamen custom | Tam kontrol, özelleştirme esnekliği |
| Ana dil (kontrol) | .NET Core | Geliştirici uzmanlığı, güçlü ekosistem |
| Agent dili | Python | AI kütüphaneleri, LiteLLM |
| LLM Gateway | LiteLLM | Tüm sağlayıcılara tek API |
| Inter-service | gRPC + Protobuf | Hızlı, tip güvenli, streaming |
| Event bus | PostgreSQL LISTEN/NOTIFY | Ek altyapı yok, yeterli throughput |
| Veritabanı | PostgreSQL | JSONB, NOTIFY, ölçeklenebilir |
| Kod çalıştırma | Docker (Strategy Pattern) | Güvenli + değiştirilebilir |
| Coder yaklaşımı | Dinamik worker pool | Ölçeklenebilir, paralel çalışma |
| İş dağıtımı | Self-pick (worker seçer) | Otonom, bağlam avantajı |
| Paralel kontrol | Hard file locking | Conflict önleme |
| Agent iletişimi | Hybrid (Orchestrator + Event) | State takibi + esneklik |
| Repo yapısı | Monorepo | Tek geliştirici, paylaşılan proto |
| İlk faz input | CLI | Hızlı başlangıç, sonra web + telegram |
| Git workflow | Feature branch per task | İzlenebilirlik, rollback |
| Agent geçmişi | Instance başına ayrı | Performans analizi, öğrenme |
| Model yönetimi | Şimdi: kullanıcı, İleride: Model Manager | Kademeli otomasyon |
