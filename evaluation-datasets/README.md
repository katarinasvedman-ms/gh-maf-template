# Evaluation Datasets

This folder contains dataset-driven evaluation scenarios in JSONL format.

## Schema

Each line must be a JSON object with:

- `name` (string, required)
- `prompt` (string, required)
- `expectedSubstring` (string, required)
- `category` (string, optional): `Normal`, `Edge`, or `Adversarial`
- `expectedTool` (string, optional)
- `expectedSuccess` (boolean, optional)
- `expectedErrorCode` (string, optional)
- `expectApprovalRequired` (boolean, optional)
- `maxLatencyMs` (number, optional)

## Default Dataset

`translator-scenarios.jsonl` includes normal, edge, and adversarial translator scenarios.
