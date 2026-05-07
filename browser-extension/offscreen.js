chrome.runtime.onMessage.addListener((msg) => {
  if (msg.type === 'copy') {
    const textarea = document.getElementById('copy-area');
    textarea.value = msg.text;
    textarea.select();
    document.execCommand('copy');
  }
});
