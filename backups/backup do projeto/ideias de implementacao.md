# 🚀 Daily Dash: Plano de Implementação Técnica

Este documento detalha o fluxo de funcionamento e a lógica das novas funcionalidades para o Daily Dash, unindo a visão estratégica com a implementação técnica.

---

## 1. O Sistema de Hierarquia (O "Cérebro")
O maior desafio e a ideia mais forte aqui é o **Link entre Metas**.

*   **Nível 1 (Mensal):** Grandes objetivos divididos em "Slices" (fatias) semanais.
*   **Nível 2 (Semanal):** As fatias do mês aparecem aqui automaticamente. Você escolhe quais sub-metas da semana vão para o "Hoje".
*   **Nível 3 (Diário):** Onde a execução acontece.

| Função Sugerida | Fluxo de Funcionamento | Lógica Técnica |
| :--- | :--- | :--- |
| `SyncGoalHierarchy()` | conseguir definir se quer que a submeta seja vinculada ou não a cada semana em especifico | Usa IDs únicos (ex: `ID:M123`) para rastrear a origem no Markdown. |
| `LinkToDailyTask()` | possibilidade de enviar ou não (escolha do usuario) uma sub-meta semanal para o dia de hoje. | Cria referência `[[Metas/Semana_09#Submeta1]]` no arquivo diário. |
| `UpdateParentProgress()` | Concluir no dia atualiza a meta pai automaticamente. | Ao salvar, busca o ID pai e incrementa o progresso no arquivo original. |

---

## 2. O Modo "Deep Dive" (A Área Central Dinâmica)
Sua ideia de dar double-click e a área central mudar é excelente para o foco.

*   **A Transição:** Ao entrar na tarefa, o "Scratchpad" (anotações gerais) dá lugar ao **"Task Notepad"**.
*   **Conteúdo do Bloco da Tarefa:**
    *   Título destacado no topo e Botão "Voltar" (Back-to-Dash).
    *   Uma área que pode conter sub-tarefas e notas, onde vc tem o texto corrido e se quiser consegue adicionar sub-tarefas dentro do texto corrido e onde o titulo da sub tarefa o botão de chek e o slider de progresso. 
*   **Expansão do Scratchpad no modo de foco do dia:** Um botão de expandir no canto superior direito faz as notas ocuparem todo o centro (aba de foco), mantendo o calendário e as listas laterais.

| Função Sugerida | Fluxo de Funcionamento | Lógica Técnica |
| :--- | :--- | :--- |
| `NavigateToTaskDetail()` | Double-Click na tarefa inicia o modo focado. | Troca o `ContentControl` principal de `MainView` para `TaskDetailMode`. |
| `RenderTaskWidgets()` | Transforma linhas específicas do MD em widgets visuais (Check + Slider) no meio do texto. | Detecta o padrão `- [ ] Nome | XP%`. O app renderiza o widget, mas salva como texto puro para manter a legibilidade do `.md`. |
| `SaveTaskNotes()` | Salva anotações e o estado dos widgets integrados. | Armazena em bloco dedicado no MD do dia: `### [Tarefa] Notas`. |
| `ExpandScratchpad()` | Expande as notas para ocupar todo o centro. | Oculta temporariamente a lista de tarefas diárias no painel central. |

---

## 3. Inteligência de Progresso (Lógica de Barras)
*   **Cálculo Automático:** `Progresso = (Sub-tarefas Concluídas / Total) * 100`.
*   **Sobrescrita Manual:** Se o usuário arrastar a barra ou digitar um número, o app "trava" o cálculo automático e respeita a decisão do usuário.
*   **Dica Visual:** Adicionar um ícone ou cor diferente quando estiver em "Modo Manual".

| Função Sugerida | Fluxo de Funcionamento | Lógica Técnica |
| :--- | :--- | :--- |
| `AutoCalculateProgress()` | Monitora as checklists dentro das notas da tarefa. | Atualiza o slider visual em tempo real baseado em `[x]`. |
| `OverrideProgressManual()` | Intervenção do usuário desativa automação. | Define flag `isManual: true` no metadata da tarefa no Markdown. |
| `ResetToAuto()` | Botão para voltar ao cálculo automático. | Remove a flag `isManual` e re-trabalha os dados das checklists. |

---

## 4. Refinamento de Interface (UX Pro)
*   **Tag Dropdown:** Substituir o texto por uma seleção visual (com cores para cada tag).
*   **Drag & Drop de Metas:** Reordenar prioridade visualmente.
*   **Prazos no Calendário:** Marcadores visuais (pontinhos ou barras coloridas) nos dias de entrega.
*   **Ajuste de Áreas (Splitters):** Permitir redimensionar as colunas (dar mais espaço para notas ou calendário).

| Função Sugerida | Fluxo de Funcionamento | Lógica Técnica |
| :--- | :--- | :--- |
| `PopulateTagDropdown()` | Escolha de tags via menu visual. | `ComboBox` vinculado a `ObservableCollection<Tag>`. |
| `InitializeDragDrop()` | Arrastar itens para mudar ordem. | Eventos `PreviewMouseMove` e `Drop` na lista de metas. |
| `ApplyCalendarMarkers()` | Destaque de dias com prazos. | `StyleSelector` que checa "Deadlines" no arquivo de metas. |
| `NavigateHeatmapHistory()` | Clicar num quadrado do Heatmap carrega o histórico daquele dia. | Evento `MouseDown` no quadrado que atualiza a `SelectedDate` e dispara o `LoadDayData()`. |
| `AdjustAreaResize()` | Redimensionamento manual de painéis. | Implementação de `GridSplitter` entre as seções da UI. |

---

## 5. Análise e Insights (Dados de Produtividade)
*Objetivo: Transformar o esforço registrado em dados para melhoria contínua.*

| Função Sugerida | Fluxo de Funcionamento | Lógica Técnica |
| :--- | :--- | :--- |
| `GenerateProductivityInsights()` | Painel que mostra % de tarefas por tag e metas concluídas. | Varredura em lote (Batch scan) de todos os `.md` para contar ocorrências de tags e estados `[x]`. |
| `VisualizeEffortDistribution()` | Gráfico simples (Pizza ou Barras) da semana/mês. | Agrupamento de dados via LINQ no C# e exibição em um mini-gráfico na UI. |

---

## 6. Utilitários e Busca Global
*Objetivo: Facilidade de acesso a informações antigas e navegação rápida.*

| Função Sugerida | Fluxo de Funcionamento | Lógica Técnica |
| :--- | :--- | :--- |
| `GlobalMarkdownSearch()` | Busca palavras-chave em todos os arquivos do projeto. | Varredura recursiva de arquivos usando `Directory.EnumerateFiles` com filtros de texto. |
| `PreviewSearchResults()` | Lista de resultados que, ao clicar, abre a tarefa ou nota correspondente. | Gera links dinâmicos para abrir o `Deep Dive` ou o `Scratchpad` no arquivo encontrado. |

---


