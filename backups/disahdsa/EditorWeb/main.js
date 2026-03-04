import { Crepe } from '@milkdown/crepe';
import { replaceAll } from '@milkdown/kit/utils';
import '@milkdown/crepe/theme/common/style.css';
// You can switch to frame.css or classic.css depending on the look you want
import '@milkdown/crepe/theme/frame-dark.css';

let editorInstance = null;
let isInternalUpdate = false;

async function initEditor() {
    const app = document.getElementById('app');

    let initialMarkdown = '';

    // Wait for native C# app to provide the initial data
    if (window.chrome && window.chrome.webview) {
        initialMarkdown = await new Promise((resolve) => {
            const initListener = (event) => {
                let msg = event.data;
                if (typeof msg === 'string') {
                    try { msg = JSON.parse(msg); } catch { }
                }

                if (msg && msg.type === 'load') {
                    window.chrome.webview.removeEventListener('message', initListener);
                    resolve(msg.data);
                }
            };

            window.chrome.webview.addEventListener('message', initListener);
            window.chrome.webview.postMessage({ type: 'ready' });
        });
    }

    // Create the Crepe editor instance
    const crepe = new Crepe({
        root: app,
        defaultValue: initialMarkdown || '',
        features: {
            [Crepe.Feature.BlockEdit]: true,
            [Crepe.Feature.CodeBlock]: true,
            // Disable features we don't want or need custom behavior for
        },
        featureConfigs: {
            [Crepe.Feature.ImageBlock]: {
                onUpload: async (file) => {
                    return new Promise((resolve) => {
                        const reader = new FileReader();
                        reader.onload = () => {
                            const base64 = reader.result.split(',')[1];
                            const extension = file.name.split('.').pop();
                            const id = "img_" + Date.now() + "_" + Math.floor(Math.random() * 1000);

                            if (window.chrome && window.chrome.webview) {
                                const uploadListener = (event) => {
                                    let msg = event.data;
                                    if (typeof msg === 'string') {
                                        try { msg = JSON.parse(msg); } catch { }
                                    }
                                    if (msg && msg.type === 'uploadResponse' && msg.id === id) {
                                        window.chrome.webview.removeEventListener('message', uploadListener);
                                        resolve(msg.url);
                                    }
                                };
                                window.chrome.webview.addEventListener('message', uploadListener);

                                window.chrome.webview.postMessage({
                                    type: 'uploadImage',
                                    id: id,
                                    extension: extension,
                                    base64: base64
                                });
                            } else {
                                resolve(URL.createObjectURL(file)); // Fallback if no webview
                            }
                        };
                        reader.readAsDataURL(file);
                    });
                }
            }
        }
    });

    // We could apply a dark theme or allow customization here if Crepe supports it nicely
    // By default, crepe adapts to system or we can override CSS variables.

    // Listen for changes and notify WPF host
    crepe.on((listener) => {
        listener.markdownUpdated((ctx, markdown, prevMarkdown) => {
            if (!isInternalUpdate && window.chrome && window.chrome.webview) {
                if (markdown !== prevMarkdown) {
                    window.chrome.webview.postMessage({
                        type: 'save',
                        data: markdown
                    });
                }
            }
        });
    });

    await crepe.create();
    editorInstance = crepe;
}

// Expose a method to set markdown from WPF
window.setMarkdown = (md) => {
    if (editorInstance) {
        // Use getMarkdown (if function) or direct fallback if undefined
        const currentMd = (typeof editorInstance.getMarkdown === 'function')
            ? editorInstance.getMarkdown()
            : "";

        if (currentMd.trim() !== md.trim()) {
            isInternalUpdate = true;
            try {
                // In Crepe / Milkdown v7, the true live update action is replaceAll
                editorInstance.editor.action(replaceAll(md, true));
            } catch (err) {
                console.error("Milkdown action failed:", err);
                if (typeof editorInstance.setMarkdown === 'function') {
                    editorInstance.setMarkdown(md);
                }
            } finally {
                // Milkdown v7 Crepe delays its markdownUpdated event (usually via requestAnimationFrame or throttle)
                // We MUST wait before unlocking, otherwise Milkdown sends the new markdown right back
                // to C#, creating a destructive loop if C# and Editor states drift.
                setTimeout(() => {
                    isInternalUpdate = false;
                }, 150);
            }
        }
    }
};

window.insertTextAtCursor = (text) => {
    if (editorInstance) {
        // With milkdown we might need to use prosemirror transaction, 
        // but simplest for an image drop is to just append or replace
        // For simplicity, we can just grab current and append if we can't easily access the cursor
        // Wait, Crepe gives access to editor. Let's do a simple append for now or handle in C# by appending.
        // Actually, let's let WPF handle it by using setMarkdown or doing it via C#.
    }
};

initEditor();
