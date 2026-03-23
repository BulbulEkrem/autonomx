# AutoNomX — Hızlı Başlangıç Rehberi

Bu rehber seni adım adım yönlendirir. Sırayla takip et.

---

## Ön Gereksinimler

Bunların kurulu olduğundan emin ol:

```bash
# .NET 8 SDK
dotnet --version        # 8.0+ olmalı

# Python 3.11+
python --version        # 3.11+ olmalı

# Docker
docker --version        # 20+ olmalı

# GitHub CLI
gh --version            # 2.0+ olmalı
gh auth status          # Authenticated olmalı

# Git
git --version
```

---

## Adım 1: Repo'yu Klonla

```bash
git clone https://github.com/KULLANICI_ADIN/autonomx.git
cd autonomx
```

---

## Adım 2: Hazırlık Dosyalarını Kopyala

Bu proje ile birlikte gelen dosyaları repo'ya kopyala:

```bash
# CLAUDE.md → repo kök dizinine
cp /path/to/CLAUDE.md ./CLAUDE.md

# ARCHITECTURE.md → docs/ dizinine
mkdir -p docs
cp /path/to/docs/ARCHITECTURE.md ./docs/ARCHITECTURE.md

# .gitignore → repo kök dizinine
cp /path/to/.gitignore ./.gitignore

# Setup script → scripts/ dizinine
mkdir -p scripts
cp /path/to/scripts/setup-github.sh ./scripts/setup-github.sh
chmod +x ./scripts/setup-github.sh
```

---

## Adım 3: İlk Commit

```bash
git add -A
git commit -m "docs: add architecture documentation and project setup files"
git push origin main
```

---

## Adım 4: GitHub Milestones, Labels ve Issues Oluştur

```bash
# Script'i çalıştır
./scripts/setup-github.sh
```

Bu script otomatik olarak:
- 20+ label oluşturur (katman, tip, agent, öncelik)
- 10 milestone oluşturur (M0-M9)
- 20+ issue oluşturur (M0 detaylı, M1-M9 özet)

---

## Adım 5: Claude Code ile Geliştirmeye Başla

```bash
# Claude Code'u başlat
claude

# İlk komutun:
> M0 milestone'ını implement et. Issue #1'den başla (monorepo klasör yapısı).
> CLAUDE.md ve docs/ARCHITECTURE.md dosyalarını referans al.
```

---

## Çalışma Döngüsü

Her issue için:

```bash
# 1. Issue'yu Claude Code'a ver
> Issue #3'ü implement et: gRPC proto dosyalarını yaz

# 2. Claude Code kodu yazar

# 3. Review et, gerekirse düzeltme iste

# 4. Commit ve push
git add -A
git commit -m "feat(M0): gRPC proto dosyaları (#3)"
git push

# 5. Issue'yu kapat
gh issue close 3

# 6. Sonraki issue'ya geç
```

---

## Milestone Tamamlama Kontrolü

Her milestone sonunda:

```bash
# Milestone durumunu kontrol et
gh issue list --milestone "M0: Project Setup & Foundation"

# Tüm issue'lar kapalıysa milestone'ı kapat
# GitHub web'den veya:
gh api repos/OWNER/REPO/milestones/1 -X PATCH -f state=closed
```

---

## Faydalı GitHub CLI Komutları

```bash
# Issue listele
gh issue list
gh issue list --milestone "M0: Project Setup & Foundation"
gh issue list --label "layer:dotnet"

# Issue detayı
gh issue view 1

# Issue oluştur
gh issue create --title "Yeni issue" --milestone "M1: .NET Core Domain & Infrastructure"

# Issue kapat
gh issue close 1
```

---

## Sorun Giderme

**gh CLI authenticated değil:**
```bash
gh auth login
```

**Proto generation hata veriyor:**
```bash
# protoc kurulu mu?
protoc --version
# Yoksa: brew install protobuf (macOS) veya apt install protobuf-compiler (Linux)
```

**Docker compose hata veriyor:**
```bash
docker-compose down -v
docker-compose up -d postgres
```
