export default function Bookmarklet() {
  return (
    <div className="bookmarklet-section">
      <h3>Browser Extension: Job URL Grabber</h3>
      <p className="subtitle">
        A lightweight Chrome/Edge extension that copies all open job tab URLs to your clipboard with one click.
      </p>

      <div className="bookmarklet-row">
        <a
          className="bookmarklet-link"
          href="/browser-extension.zip"
          download="job-url-grabber-extension.zip"
          onClick={e => {
            e.preventDefault()
            alert(
              'The extension files are in the "browser-extension" folder of this project:\n\n' +
              'C:\\Users\\lex\\source\\kiro\\jobsearch\\browser-extension\n\n' +
              'Follow the installation instructions below to load it into your browser.'
            )
          }}
        >
          📦 Get Extension Files
        </a>
        <span className="bookmarklet-hint">Extension folder: <code>browser-extension/</code> in project root</span>
      </div>

      <details className="bookmarklet-instructions">
        <summary>Installation Instructions</summary>
        <ol>
          <li>Open your browser and navigate to:
            <ul>
              <li><strong>Chrome:</strong> <code>chrome://extensions/</code></li>
              <li><strong>Edge:</strong> <code>edge://extensions/</code></li>
            </ul>
          </li>
          <li>Enable <strong>Developer mode</strong> (toggle in the top-right corner)</li>
          <li>Click <strong>"Load unpacked"</strong></li>
          <li>Navigate to: <code>C:\Users\lex\source\kiro\jobsearch\browser-extension</code></li>
          <li>Click <strong>Select Folder</strong></li>
          <li>The extension icon (blue square) will appear in your toolbar</li>
        </ol>
      </details>

      <details className="bookmarklet-instructions">
        <summary>How to Use</summary>
        <ol>
          <li>Go to LinkedIn (or Indeed/Glassdoor/ZipRecruiter) and search for jobs</li>
          <li>Open each interesting job in a new tab (Ctrl+click or right-click → Open in new tab)</li>
          <li>Once you have several job tabs open, click the extension icon in your toolbar</li>
          <li>It automatically filters to only job-related URLs and copies them to your clipboard</li>
          <li>Come back to this page, paste into the text area below</li>
          <li>Click <strong>"Add Link(s)"</strong> then <strong>"Run Import"</strong></li>
        </ol>
        <p className="bookmarklet-note">
          <strong>Tip:</strong> The extension only copies URLs from tabs that contain 
          linkedin.com/jobs, indeed.com, glassdoor.com, or ziprecruiter.com. 
          All other tabs are ignored.
        </p>
      </details>
    </div>
  )
}
