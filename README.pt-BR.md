# Prompt Registry

> Prompts são código que vai para produção — então versione, teste, promova e faça rollback como
> código. Um registry que trata um prompt como artefato de primeira classe, não como uma string num
> arquivo de código. Documentado primeiro, implementado em público.

[![Fase](https://img.shields.io/badge/fase-1%20design-blue)](./ROADMAP.md)
[![ADRs](https://img.shields.io/badge/ADRs-5-green)](./docs/adr)
[![Licença](https://img.shields.io/badge/licen%C3%A7a-MIT-lightgrey)](./LICENSE)

Um prompt é a linha que mais muda o comportamento de uma aplicação de IA, e costuma ser a menos
"engenheirada". Ele vive como um literal de string dentro de uma função, editado no lugar,
implantado junto com o código, sem versão que alguém consiga nomear, sem teste que pegue uma
regressão, e sem forma de reverter o que quebrou produção sem um redeploy.

Enquanto isso, todo mundo trata o *código* ao redor do prompt com rigor total — revisado, versionado,
testado, promovido entre ambientes. O prompt, que muda o comportamento mais do que esse código,
não recebe nada disso. Este repositório fecha essa lacuna: um prompt é um artefato versionado com
identidade, suíte de testes, caminho de promoção e rollback — a mesma disciplina que o resto da
aplicação já tem.

**English:** [README.md](./README.md)

---

## O que já existe

| Área | Status | Link |
| --- | --- | --- |
| Contexto e escopo | Pronto | [docs/context.md](./docs/context.md) |
| Diagramas de ciclo de vida | Pronto | [docs/diagrams](./docs/diagrams) |
| Registros de Decisão de Arquitetura | 5 publicados | [docs/adr](./docs/adr) |
| Por que prompts são código | Pronto | [docs/prompts-are-code.md](./docs/prompts-are-code.md) |
| Implementação do registry | Planejada — Fase 3 | [ROADMAP.md](./ROADMAP.md) |

## A ideia

**Um prompt em produção tem uma versão, e a aplicação o referencia por nome, não por literal.** No
momento em que um prompt tem identidade estável — um nome e uma versão — tudo que o código já faz se
torna possível: testar aquela versão antes de subir, promovê-la de staging para produção, e reverter
para a versão anterior em segundos sem tocar na aplicação. O registry é o que dá ao prompt essa
identidade.

Tudo [nos ADRs](./docs/adr) decorre de tratar um prompt como artefato versionado em vez de string.

## Por que documentar primeiro

As decisões caras são as que viram contratos. Como uma versão de prompt é identificada, como uma
aplicação a referencia, o que "o prompt mudou" significa para a versão — cada uma passa a sustentar
tudo no instante em que uma aplicação depende do registry, e mudá-la depois quebra toda aplicação que
referencia um prompt. E a pergunta mais difícil — como testar uma mudança em um componente
não-determinístico — é uma decisão de metodologia que molda todo o registry, e é muito mais barata de
raciocinar no papel do que de retrofitar.

> Os documentos técnicos são mantidos em inglês para alcançar o público mais amplo possível.
> Este README traz o contexto em português.

## Roadmap

Quatro fases, acompanhadas como milestones no GitHub. Detalhes em [ROADMAP.md](./ROADMAP.md).

1. **Design** — contexto, diagramas de ciclo de vida, ADRs, o argumento de que prompts são código
2. **Contratos** — o artefato de prompt, o formato de referência, o contrato de teste
3. **Registry** — o store, o fluxo de promoção, uma integração de exemplo
4. **Validação** — testes de regressão contra um golden set, drills de rollback

## Relacionados

- [enterprise-ai-framework](https://github.com/prodrigues2023/enterprise-ai-framework) — o framework cuja decisão planejada de versionamento de prompt este registry aprofunda
- [rag-evaluation-toolkit](https://github.com/prodrigues2023/rag-evaluation-toolkit) — como uma mudança de prompt é julgada melhor ou pior, que é o que gateia uma promoção
- [ai-solution-architecture-kit](https://github.com/prodrigues2023/ai-solution-architecture-kit) — onde o controle de mudança de prompt se encaixa na certificação de modelos e na revisão

## Autor

Paulo Roberto Franco Rodrigues — AI Solutions Architect.
Recentemente projetou frameworks corporativos de IA e atuou em comitê de arquitetura de IA definindo
os padrões de engenharia que trazem disciplina de software para a entrega de IA.
[LinkedIn](https://linkedin.com/in/paulo-roberto-franco-rodrigues)

## Licença

MIT — veja [LICENSE](./LICENSE).
