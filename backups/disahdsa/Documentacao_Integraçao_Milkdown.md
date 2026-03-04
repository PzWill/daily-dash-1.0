# Como Integramos o Milkdown (Crepe) com Salvamento Offline no C#

Este documento explica detalhadamente todas as mudanças necessárias que fizemos para transformar o template original e "estático" do editor de texto Milkdown (na sua variante Crepe) em um editor totalmente funcional, capaz de salvar os dados automaticamente em formato Markdown local (na pasta `dias`).

O nosso cenário era um aplicativo Desktop focado em performance (C# WPF), que exibia o aplicativo web rodando via `WebView2` (um navegador Edge embutido).

---

## 1. O Problema Inicial
Originalmente, a inicialização do Milkdown no arquivo `EditorWeb/src/main.ts` era completamente desconectada e *stateless* (sem estado):

```typescript
// COMO ERA ORIGINALMENTE NO MILKDOWN:
import { Crepe } from '@milkdown/crepe';

async function main() {
  const defaultText = `...`; // Texto padrão do milkdown
  
  const app = document.getElementById('app');
  const crepe = new Crepe({
    root: app,
    defaultValue: defaultText // Sempre carregava isso ao abrir
  });

  await crepe.create();
}
```
A página abria, carregava aquele texto de tutorial fixo e qualquer edição simplesmente morria no momento que você fechava o aplicativo. 

---

## 2. A Magia da Comunicação: WebView2 (`window.chrome.webview`)
Para fazer a página web (JavaScript) interagir silenciosamente com os dados locais do PC (sem precisar de um servidor/API), usamos a ponte de comunicação da webview nativa que é acessível pela variável `window.chrome.webview`.

Primeiro, precisamos avisar ao compilador do TypeScript (Vite) que essa variável existe no navegador em que rodaremos `main.ts`:
```typescript
declare global {
  interface Window {
    chrome: any;
  }
}
```

### 2.1 - Pedindo os dados do dia ao C# e esperando a resposta
Nós transformamos a inicialização do `crepe`. Antes de desenhar o editor na tela, o JavaScript agora "pausa" e avisa ao C# que ele está vivo e quer as anotações diárias. Ele faz isso enviando `type: 'ready'` através do `postMessage`.

Como ele depende dessa resposta, empacotamos essa espera dentro de uma `Promise`.

```typescript
// COMA AGORA É NO MILKDOWN (main.ts):

let initialMarkdown = defaultText;

// Verifica se está rodando dentro do "navegador" do nosso aplicativo C#
if (window.chrome && window.chrome.webview) {
  initialMarkdown = await new Promise((resolve) => {
    
    // 1. Cria um ouvidor para receber a mensagem do C#
    const initListener = (event: any) => {
        let msg = event.data;
        if (typeof msg === 'string') {
          try { msg = JSON.parse(msg); } catch {}
        }
        
        // 2. Se a mensagem do C# for do tipo "load", pegamos os dados e abrimos a promise
        if (msg && msg.type === 'load') {
          window.chrome.webview.removeEventListener('message', initListener);
          resolve(msg.data); // destrava a Promise passando nossa nota salva
        }
    };
    
    // Prende nosso espião na página
    window.chrome.webview.addEventListener('message', initListener);
    
    // 3. Efetivamente grita para o C#: "Pode mandar as anotações do dia de hoje!"
    window.chrome.webview.postMessage({ type: 'ready' });
  });
} 

// ... só então ele injeta no Markdown, trocando o texto default pelo seu
const crepe = new Crepe({
  root: app,
  defaultValue: initialMarkdown || ''
});
```

### 2.2 - Interceptando cada tecla para salvar em Real-Time
O Milkdown possui nativamente um event listener `markdownUpdated`. Nos tornamos ouvintes ali. Sempre que o plugin detectar que o estado (texto) do código fonte mutou em relação à versão anterior, avisamos o app C# pra gravar isso pelo `postMessage` mandando o `type: 'save'`.

```typescript
crepe.on((listener) => {
  listener.markdownUpdated((_ctx, markdown, prevMarkdown) => {
    // Se o texto mudou e estamos rodando no App Offline:
    if (markdown !== prevMarkdown && window.chrome && window.chrome.webview) {
        
      // O C# processará silenciosamente o objeto e mandará escrever no disco local
      window.chrome.webview.postMessage({
        type: 'save',
        data: markdown
      });
      
    }
  });
});
```

---

## 3. O "Cérebro" de Gravador no C# (`MainWindow.xaml.cs`)
Do lado do Backend (C# WPF), nossa WebView2 precisava aprender a reagir aos `postMessage` do Javascript. 
Nós "espetamos" um Hook chamado `WebMessageReceived` logo quando a janela inicia.

Foi aqui que criamos a função mágica secreta de localizar inteligentemente sua pasta `dias`. Por ser dependente de compilação e diretórios (`bin/Debug...`), foi necessário subir os diretórios do ambiente de runtime pra não correr o risco do C# gravar em outra pasta.

### 3.1 - Construindo a lógica do Receptor
A função `WebView_WebMessageReceived` passa a ser a API Rest local.
O JavaScript nos envia JSONs, nós quebramos ele com `JsonDocument` e vemos sob condição se é a hora de abrir (read) ou se é a hora de salvar a nota (escrever text):

```csharp
private void WebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
{
    // Pega a mensagem do Javascript de que acabamos de mandar
    string msg = e.WebMessageAsJson;

    // Converte a string pra um objeto JSON nativo do .NET
    using (System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(msg))
    {
        var root = doc.RootElement;
        string msgType = root.GetProperty("type").GetString();

        // CONDICIONAL 1: O NAVEGADOR ESTÁ PRONTO E QUER OS DADOS PRA EXIBIR!
        if (msgType == "ready")
        {
            // Pega o destino: "pasta dias / 2026-03-01.md"
            string filePath = GetTodayFilePath(); 
            string initialMarkdown = "";
            
            // Lê o disco. Se o seu arquivo ainda não existe hj, ele constrói algo base...
            if (File.Exists(filePath)) 
                 initialMarkdown = File.ReadAllText(filePath);
            else 
                 initialMarkdown = $"# Foco do Dia\n\n";
            
            // Responde para aquele Ouvidor do Javascript "initListener" destravando a aplicação com o texto correto
            var response = new { type = "load", data = initialMarkdown };
            webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(response));
        }

        // CONDICIONAL 2: O CARA DIGITOU UMA LETRA A MAIS NO MILKDOWN E ELE QUIS SALVAR
        else if (msgType == "save")
        {
            // Pega o que está contido no atributo `.data` (texto full raw do seu documento)
            string markdown = root.GetProperty("data").GetString();
            
            // Grava o arquivo local em disco em microssegundos impercepetíveis!
            File.WriteAllText(GetTodayFilePath(), markdown);
        }
    }
}
```

## Resumo da Arquitetura Funcionando:
1. `EditorApp` é executado!
2. `MainWindow` em C# abre e cria o fundo Web e registra o EventListener para monitorar o WebView2.
3. Arquivo Web compila: o JS lê o `main.ts` e diz **"Tô On C#, lança a Base"** (`type: ready`).
4. O C# abre arquivo da pasta `dias`, converte pra string e manda de volta via PostMessage pro Web.
5. Promise aceita os dados e **injeta o Markdown visualmente renderizado no Crepe** na sua tela.
6. A cada movimento lá (por frame mutado no texto): **Milkdown avisa C# em tempo real "Salva essa mudança pra mim!"** (`type: save`).
7. O C# re-escreve silenciosamente o `2026-X-Y.md` original na sua pasta.
