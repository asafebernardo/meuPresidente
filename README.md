# História dos Presidentes

Sistema de pesquisa histórica sobre os presidentes do Brasil. O site organiza mandatos, acontecimentos, atos normativos, políticas, indicadores e fontes. Ele não classifica governos e não publica uma afirmação histórica sem fonte.

A prioridade do acervo é precisão, fonte, transparência, rastreabilidade, neutralidade e usabilidade.

## Requisitos

- .NET SDK 10
- PostgreSQL 16
- Opcional: Redis e Docker

## Arquitetura

A solução `HistoriaDosPresidentes.slnx` separa as responsabilidades:

| Projeto | Papel |
| --- | --- |
| `Presidents.Domain` | Entidades, regras de data, autoria, publicação e deduplicação |
| `Presidents.Application` | Casos de uso, validação, busca, comparação e respostas com fontes |
| `Presidents.Infrastructure` | EF Core, Identity, seed, adaptadores de importação e cliente de modelo |
| `Presidents.Api` | HTTP, JWT, Swagger e limites de taxa |
| `Presidents.Web` | Site público e administração em Blazor SSR |
| `Presidents.UnitTests` | Regras de domínio e aplicação |
| `Presidents.IntegrationTests` | API, banco, autenticação e endpoints públicos |

Um presidente pode ter vários mandatos. Cada mandato guarda a própria forma de chegada e o próprio tipo de governo. Uma lei pode registrar o presidente em exercício sem que isso signifique autoria. Fato documentado, interpretação, opinião, crítica, informação jornalística e dado oficial são naturezas distintas. Opinião, crítica e interpretação exigem atribuição.

O conteúdo segue o fluxo Rascunho, Revisão, Publicado e Arquivado. Só o que está Publicado aparece no site e na API pública. Publicar presidente, mandato, acontecimento, lei, política, afirmação ou valor de indicador exige ao menos uma fonte publicada.

O catálogo público, a partir de 15/03/1985, lista leis ordinárias, leis complementares e emendas constitucionais da lista de legislação do Senado. O tema sai de palavras da ementa e fica marcado como classificação automática. O presidente em exercício é o do período que contém a data de assinatura. Os indicadores de economia e trabalho são o IPCA e o PIB do IBGE, o salário mínimo da série 1619 do Banco Central e a taxa de desocupação da PNAD Contínua. A variação descreve o período; ela não isola o efeito das leis. Saúde, educação e os outros temas mostram as leis e registram que não há série oficial carregada.

## Instalação

```bash
dotnet restore HistoriaDosPresidentes.slnx
```

Crie o banco e exporte a conexão. A senha não fica no `appsettings.json`.

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=historia_presidentes;Username=postgres;Password=postgres"
export Database__ApplyMigrations=true
export Seed__ApplyDemoData=true
export Seed__AdminEmail="admin@historiapresidentes.local"
export Seed__AdminPassword="ChangeMe_Dev_Only_123!"
export Jwt__Key="DevelopmentOnly_NotForProduction_JwtKey_32!"
```

Troque a senha do administrador e a chave JWT fora de uma máquina local.

## Variáveis de ambiente

| Variável | Uso |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | PostgreSQL |
| `ConnectionStrings__Redis` | Cache distribuído. Sem ela, o processo usa memória |
| `Database__ApplyMigrations` | Aplica as migrations na inicialização |
| `Seed__ApplyDemoData` | Carrega a semente de demonstração se o acervo ainda estiver vazio |
| `Seed__ApplyLawCatalog` | Importa leis do Senado desde 15/03/1985 e as séries oficiais de indicadores. Em Development, o padrão é ligar |
| `Seed__AdminEmail` e `Seed__AdminPassword` | Cria o papel Admin e o primeiro usuário. Sem as duas, nenhum administrador é criado |
| `Jwt__Key` | Assinatura JWT, com no mínimo 32 caracteres. Obrigatória fora de Development |
| `AI__ApiKey` | Chave do modelo. Sem ela, as respostas usam apenas o texto recuperado no banco |
| `Ai__BaseUrl` e `Ai__Model` | Endpoint e modelo, com padrão compatível com a API da OpenAI |
| `Public__CanonicalBaseUrl` | URL canônica do site, usada em sitemap e metatags |
| `Cors__Origins` | Origens permitidas pela API |

Em Development, a API e o site aplicam migrations e a semente quando essas chaves não forem definidas. A chave JWT de desenvolvimento só é aceita nesse ambiente.

## Banco e migrations

A migration inicial está em `src/Presidents.Infrastructure/Persistence/Migrations`. Há índices para nome e slug de presidente, data e título de acontecimento, número e ano de lei, URL de fonte e slug de categoria.

Para gerar uma nova migration a partir da infraestrutura:

```bash
dotnet tool install --global dotnet-ef --version 10.0.12
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add NomeDaMudanca \
  --project src/Presidents.Infrastructure \
  --startup-project src/Presidents.Infrastructure \
  --output-dir Persistence/Migrations
```

A fábrica de design exige `ConnectionStrings__DefaultConnection`. Ela não embute senha.

## Execução local

```bash
dotnet run --project src/Presidents.Api
dotnet run --project src/Presidents.Web
```

- API: `http://localhost:5080`
- Swagger: `http://localhost:5080/swagger`
- Site: `http://localhost:5088`
- Saúde: `http://localhost:5080/health`

Os perfis de lançamento estão em `Properties/launchSettings.json`.

## Docker

```bash
export ADMIN_PASSWORD="uma-senha-longa"
export JWT_KEY="uma-chave-com-pelo-menos-32-caracteres"
docker compose up --build
```

O Compose sobe `postgres`, `redis`, `api` e `web`. As portas publicadas são 5432, 6379, 5080 e 5088. A senha padrão do Compose é apenas para desenvolvimento e deve ser substituída.

## Testes

```bash
dotnet test HistoriaDosPresidentes.slnx
```

Os testes de integração recriam o banco `historia_presidentes_test` em `localhost:5432`, com usuário e senha `postgres`. Eles não usam o banco de desenvolvimento.

## Site

A página inicial, a linha do tempo, as categorias, as leis, os acontecimentos, a comparação e a pesquisa leem somente conteúdo publicado. A comparação aceita até quatro presidentes, mostra tabelas e gráficos quando há série publicada e não produz ranking.

A pergunta ao acervo segue o fluxo: pergunta, recuperação no banco, seleção opcional de fontes já recuperadas e resposta composta apenas por afirmações armazenadas. Se não houver evidência suficiente, a resposta é:

> Não encontrei fontes suficientes no banco para afirmar isso.

O modelo, quando `AI__ApiKey` existe, só pode escolher identificadores de fonte já recuperados. Um identificador desconhecido é descartado. Se o modelo falhar, a resposta volta a usar toda a evidência recuperada. O sistema não inventa URL, data ou referência.

## Administração

Os papéis são Admin, Editor e Revisor.

- Editor envia rascunho para revisão.
- Revisor publica, devolve para rascunho ou arquiva.
- Admin também pode publicar direto a partir do rascunho e excluir.
- A entrada do site fica em `/admin/entrar` e usa cookie com proteção antifalsificação.
- A API usa JWT.

## Como adicionar uma fonte de importação

1. Implemente `IDataImporter` com um `ImportChannel` próprio, ou reutilize Legislação, Notícia e Acadêmico.
2. Registre o adaptador na injeção de dependência da infraestrutura.
3. Calcule o hash do conteúdo e grave um `IngestionRecord` com URL, data da coleta e identificador externo.
4. Recuse duplicata por hash, URL ou identificador externo.
5. Grave o registro como rascunho. Não publique na importação.
6. Para legislação, aceite apenas os domínios de `Ingestion:AllowedHosts`. O padrão inclui Planalto, Diário Oficial, Senado, Câmara, Biblioteca da Presidência, Banco Central, IBGE, Ipea e TSE.
7. Para notícia ou texto acadêmico, guarde título, URL, veículo, autoria, data, trecho de até 400 caracteres e resumo próprio. Não armazene o artigo integral.

Os adaptadores atuais são `GovernmentLegislationImporter`, `NewsImporter` e `AcademicSourceImporter`. A importação é feita por serviço, não por controller.

## Semente de demonstração

A semente traz categorias, indicadores sem números e três presidentes cujos textos estão ligados a páginas oficiais consultadas na elaboração do cadastro. Esses registros são marcados como demonstração. Eles não esgotam os mandatos e não incluem estatística, lei ou data que não esteja nessa base. A Lei 8.080/1990 está catalogada sem presidente vinculado.

## Neutralidade

Evite linguagem de juízo no cadastro. Um resultado numérico fica no indicador, com fonte. Uma avaliação fica numa afirmação com autoria identificada. Quando as fontes divergem, use o campo de divergência e mantenha as fontes correspondentes.
