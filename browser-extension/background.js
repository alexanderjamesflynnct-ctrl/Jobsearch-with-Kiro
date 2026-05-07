chrome.action.onClicked.addListener(async () => {
  // Get all open tabs
  const tabs = await chrome.tabs.query({});
  
  // Filter to job-related URLs only
  const jobUrls = tabs
    .map(tab => tab.url)
    .filter(url => 
      url.includes('linkedin.com/jobs') ||
      url.includes('indeed.com') ||
      url.includes('glassdoor.com') ||
      url.includes('ziprecruiter.com')
    );

  if (jobUrls.length === 0) {
    // If no job URLs found, copy ALL tab URLs and let the import page filter
    const allUrls = tabs.map(tab => tab.url).filter(url => url.startsWith('http'));
    await copyToClipboard(allUrls);
    chrome.notifications.create({
      type: 'basic',
      iconUrl: 'icon.png',
      title: 'Job URL Grabber',
      message: `No job URLs found. Copied ${allUrls.length} tab URL(s) to clipboard.`
    });
  } else {
    await copyToClipboard(jobUrls);
    chrome.notifications.create({
      type: 'basic',
      iconUrl: 'icon.png',
      title: 'Job URL Grabber',
      message: `Copied ${jobUrls.length} job URL(s) to clipboard! Paste into Import Links.`
    });
  }
});

async function copyToClipboard(urls) {
  const text = urls.join('\n');
  
  // Use offscreen document to access clipboard
  try {
    await chrome.offscreen.createDocument({
      url: 'offscreen.html',
      reasons: ['CLIPBOARD'],
      justification: 'Copy tab URLs to clipboard'
    });
  } catch (e) {
    // Document may already exist
  }
  
  await chrome.runtime.sendMessage({ type: 'copy', text });
}
