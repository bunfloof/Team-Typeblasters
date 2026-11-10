"use client";
// Copies text to the clipboard; true when it worked.
// Browsers only offer navigator.clipboard on https:// pages and on localhost,
// so on the server's plain http:// address (http://216.219.90.186:25731) the
// older way is used: select the text in a hidden text box and "copy".
export async function copyText(text: string): Promise<boolean> {
  if (navigator.clipboard && window.isSecureContext) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      // permission refused: try the older way below
    }
  }
  const focused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  const box = document.createElement("textarea");
  box.value = text;
  box.setAttribute("readonly", "");
  box.style.position = "fixed";
  box.style.top = "0";
  box.style.left = "0";
  box.style.opacity = "0";
  document.body.appendChild(box);
  box.select();
  let copied = false;
  try {
    copied = document.execCommand("copy");
  } catch {
    copied = false;
  }
  box.remove();
  focused?.focus();
  return copied;
}
