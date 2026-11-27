# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Modo portfólio (`Portfolio:Habilitado`): a versão pública expõe apenas o fluxo operacional do produto (dashboard, atendimento, clientes, relatórios e configurações); áreas de back-office (farmácias, administradores, campanhas), integrações (canais) e o harness interno de testes ficam ocultas do menu e bloqueadas por redirecionamento.
- Extração do nome de contato (incluindo o novo username configurado pela Meta) via `contacts[].profile.name` no webhook oficial da WhatsApp Cloud API, aplicado na criação/atualização de `Cliente`, alinhando o comportamento com o fluxo Evolution Go.
### Changed
- Todos os projetos migrados de `net10.0` para `net11.0` (.NET 11).
### Deprecated
### Removed
### Fixed
### Security
- Histórico do Git recriado para não conter arquivos sensíveis; segredos seguem fora do repositório (`.env`, `appsettings.Local.json`, `appsettings.Development.json` e `uploads/` ignorados).