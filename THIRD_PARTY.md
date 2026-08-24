# Third-Party Software

## WeakApp (WeakAppApi)

- **Upstream repository**: https://github.com/nantonov/WeakApp
- **Vendored commit**: `c6451dc54c65e066ff8f42671bb99461793845fd` (2025-09-10)
- **Location in this repo**: `third_party/weak-app/` (Dockerfile + prebuilt `publish/` output,
  sufficient to `docker build` without network access)
- **License**: MIT

### License text (as published in the upstream repository)

```
MIT License

Copyright (c) 2025 Mikita

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### Notes

- `third_party/weak-app/publish/` is the upstream repository's own prebuilt output, vendored
  byte-for-byte (including its accidental nested `publish/publish/publish/` sub-directories,
  which are an artifact of the upstream repo itself, not introduced here).
- Upstream's own `.github/workflows/` and `.gitignore` were intentionally not vendored — they
  describe the upstream repo's own CI, not ours.
- Runtime contract (endpoints, headers): see upstream `README.md` copied alongside the
  Dockerfile — `GET /meters`, `GET /health`, `GET /healthz`, `GET /.well-known/health`, header
  `X-Api-Key: supersecret`. Full observed-response verification is TASK-005's job, not this one.
