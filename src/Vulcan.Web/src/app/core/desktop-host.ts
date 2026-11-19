/** A message listener for the desktop app's WebView2 bridge. */
type DesktopMessageListener = (event: MessageEvent) => void;

/** The WebView2 bridge the desktop app provides (ADR-004). */
export interface DesktopWebView {
  postMessage(message: unknown): void;
  addEventListener(type: 'message', listener: DesktopMessageListener): void;
  removeEventListener(type: 'message', listener: DesktopMessageListener): void;
}

/** The desktop app's WebView2 bridge, or undefined in a browser (`npm start`). */
export function desktopWebView(): DesktopWebView | undefined {
  return (window as Window & { chrome?: { webview?: DesktopWebView } }).chrome?.webview;
}
