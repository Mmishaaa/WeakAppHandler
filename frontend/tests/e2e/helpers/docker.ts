import { execFileSync } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

// frontend/tests/e2e/helpers -> repo root, where docker-compose.yml lives.
const repoRoot = path.resolve(fileURLToPath(new URL('.', import.meta.url)), '../../../..')

/**
 * Genuinely stops/starts a real container of the running compose stack (S2/S3) rather than just
 * documenting the step - `execFileSync` (not `exec`) so a service name can never be interpreted as
 * shell syntax.
 */
export function stopService(service: string): void {
  execFileSync('docker', ['compose', 'stop', service], { cwd: repoRoot, stdio: 'pipe' })
}

export function startService(service: string): void {
  execFileSync('docker', ['compose', 'start', service], { cwd: repoRoot, stdio: 'pipe' })
}
