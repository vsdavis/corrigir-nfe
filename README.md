# corrigir-nfe

Programa com tela que analisa o XML da NF-e **antes da transmissão**, corrige o
que é objetivo e aponta o que depende de decisão fiscal. Feito para o próprio
cliente usar: escolhe a nota, clica em **Corrigir nota**, transmite de novo.

É uma solução provisória até a totalização ser corrigida no sistema emissor.

Nasceu de cinco notas da Ladopet rejeitadas entre 13 e 16/10/2026, todas pelo mesmo
conjunto de defeitos no XML gerado pelo FlexCorp.

---

## Compilar

```
dotnet publish -c Release
```

Sai um executável único em `bin/Release/net8.0-windows/win-x64/publish/corrigir-nfe.exe`.
É só esse arquivo que vai para a máquina do cliente — não precisa instalar .NET.

## Usar

1. **Primeira vez:** o programa abre pedindo duas pastas — onde o sistema grava os
   XMLs das notas e onde guardar os backups. A configuração fica em
   `%LocalAppData%\CorrigirNFe\config.ini` (vale por usuário do Windows).
2. **Dia a dia:** a lista mostra as notas da pasta, da mais recente para a mais
   antiga. As que precisam de correção aparecem em vermelho; a tela já abre nelas.
   Quando o sistema grava uma nota nova, ela aparece sozinha.
3. Selecionar a nota → **Corrigir nota** → voltar ao sistema e transmitir de novo.

Outras formas de abrir uma nota: arrastar o XML para a janela, ou para cima do
ícone do `.exe`.

| Botão | O que faz |
|---|---|
| Corrigir nota | backup do original, corrige o arquivo **no mesmo lugar**, confere o resultado |
| Restaurar original | volta a nota para o último backup dela |
| Abrir pasta de backup | abre o Explorer já com o backup da nota selecionado |
| Configurações | troca as pastas |

### Segurança da correção

- O original é copiado para `<backup>\AAAA-MM-DD\HHMMSS_<arquivo>.xml` **antes** de
  qualquer alteração, e a cópia é conferida. Se o backup falhar, a nota não é tocada.
- A versão corrigida é gravada num `.tmp` ao lado e trocada de uma vez
  (`File.Replace`): o sistema emissor nunca lê um arquivo pela metade.
- Se a nota estiver aberta em outro programa, nada é alterado e o usuário é avisado.
- Cada correção e restauração fica registrada em `<backup>\historico.log`
  (data, nota, arquivo, usuário e máquina).
- A pasta de backup não pode ser a mesma das notas.

---

## O que ele corrige sozinho

Só o que é objetivo, ou seja, o que tem uma resposta única e verificável:

**Estrutura**
- ordem das tags em cada grupo, conforme o schema oficial
- `indPres` e `indIntermed` ausentes
- tags opcionais enviadas em branco (`CEST`, `xPed`, `nItemPed`, …)
- grupos opcionais totalmente vazios (`entrega`, `retirada`)
- tags que não existem mais no grupo (`vBCSTRet`/`vICMSSTRet` no `ICMSTot`)
- máscara na Inscrição Estadual (`13.904.578-3` → `139045783`)

**Cálculos**
- `vProd` = `qCom × vUnCom`, arredondado a 2 casas
- `vItem` e base do IBS/CBS espelhando o `vProd`
- `vICMS`, `vIBSUF`, `vIBSMun`, `vIBS`, `vCBS` a partir da base e da alíquota declaradas
- **todos os totais**, somando os valores **já arredondados item a item**
- `vNFTot` = `vNF + IBS + CBS + IS` (não é a soma dos itens)

## O que ele nunca altera

CST, CFOP, alíquota, `natOp`, `indFinal` — nada que mude a tributação da nota.
Esses casos saem no relatório em **DECISÃO FISCAL**, para alguém decidir.

Também sai como **REVISÃO MANUAL** o que o programa não sabe resolver: tag
obrigatória em branco, tag estranha com conteúdo dentro de um grupo.

---

## A regra que causou tudo isso

A SEFAZ soma os valores **já arredondados** de cada item. O FlexCorp soma os
valores exatos e arredonda no fim. Os dois resultados divergem sempre que as
frações de centavo descartadas em cada item cruzam a fronteira do meio centavo:

```
soma exata dos itens          1514,0511  → arredonda → 1514,05   (FlexCorp enviou)
soma dos vProd já arredondados                         1514,06   (SEFAZ espera)
```

Daí a rejeição "Total do Produto / Serviço difere do somatório dos itens".

Há um segundo defeito, visto na NF 443: `90 × 1,6785 = 151,065` caiu em
`vProd` como `151,07` e em `vItem` como `151,06` — o mesmo número arredondado
de dois jeitos dentro do mesmo item. É o sintoma clássico de um dos caminhos
estar calculando em `float` em vez de decimal.

Por isso `Corretor.R2()` usa `decimal` com `MidpointRounding.AwayFromZero`,
e o código não usa `double` em lugar nenhum. **Se for portar esta lógica para
o FlexCorp, esse é o ponto que importa.**

---

## Arquivos

| Arquivo | Conteúdo |
|---|---|
| `Layout.cs` | ordem canônica dos grupos, extraída do schema oficial PL_010_V1.30 |
| `Corretor.cs` | leitura, correções e recálculo |
| `Ocorrencia.cs` | modelo dos achados do relatório |
| `Servico.cs` | analisar, corrigir com backup, restaurar, histórico |
| `Config.cs` | pastas configuradas e validação |
| `Relatorio.cs` | texto do painel de detalhes (resumo + detalhes técnicos) |
| `FormPrincipal.cs` / `FormConfig.cs` / `Ui.cs` | telas |
| `Program.cs` | ponto de entrada |

`Layout.cs` é gerado a partir de `leiauteNFe_v4.00.xsd` + `DFeTiposBasicos_v1.00.xsd`.
Quando sair uma nova NT que mude o layout, é esse arquivo que precisa ser
regerado — a lógica em si não muda.

Grupos cujo nome aparece no schema com conteúdos diferentes conforme o contexto
(`IPI`, `gIBS`, `gIBSUF`, `gIBSMun`, `gCBS`) ficam **de fora** da reordenação de
propósito: reordenar pelo nome poderia embaralhar o grupo errado.

---

## Cuidados

- **XML assinado:** a assinatura é removida (o conteúdo mudou) e a nota precisa
  ser **assinada de novo** antes de transmitir. O programa avisa. Os XMLs que o
  sistema deixa na pasta antes de transmitir vêm sem assinatura — o emissor assina
  na hora do envio —, então no uso normal isso não acontece.
- **Nota já autorizada (`nfeProc`):** recusada de propósito. Nota autorizada não
  se corrige — o caminho é cancelamento ou carta de correção.
- **XML copiado da aba do navegador:** funciona (o aviso do Chrome antes do XML é
  descartado), mas o certo é pegar o arquivo direto da pasta do FlexCorp.

---

## Testes

Validado contra as cinco notas reais (1601, 1606, 207, 443, 1857 — quatro filiais).
Em todas, a saída bate com a correção feita manualmente e valida sem nenhum erro
contra `nfe_v4.00.xsd`, à exceção da assinatura, que o emissor aplica depois.

| Nota | Itens | Correção nos totais |
|---|---|---|
| 1601 | 55 | vProd 15.046,37 → 15.046,36 · vICMS 2.448,66 → 2.448,68 |
| 1606 | 12 | vProd 1.514,05 → 1.514,06 · vICMS 238,09 → 238,08 |
| 207 | 11 | vProd 1.223,13 → 1.223,15 · vICMS 95,50 → 95,51 |
| 443 | 4 | vProd 498,27 → 498,28 (+ `vItem` do item 1) |
| 1857 | 13 | vProd 1.737,09 → 1.737,06 · vICMS 295,31 → 295,32 |

Rodar o programa na própria saída não muda mais nada (idempotente).
