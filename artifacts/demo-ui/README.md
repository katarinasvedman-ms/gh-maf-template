# Run Artifact Viewer (Local Demo)

Start the local demo UI server from this folder:

```powershell
./run-demo-ui.ps1
```

Optional custom port:

```powershell
./run-demo-ui.ps1 -Port 5510
```

The script serves this directory and opens the browser to:

- http://localhost:5507/

The page auto-loads:

- ./evaluation-report.json
- ./evaluation-library-report.json

The local server maps these UI endpoints to:

- artifacts/evaluation-report.json
- artifacts/evaluation-library-report.json

Stop the server with `Ctrl+C`.
