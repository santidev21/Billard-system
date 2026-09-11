import { spawn, spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const mode = (process.argv[2] ?? 'all').toLowerCase();
const extraArgs = process.argv.slice(3);
const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const rootDirectory = path.resolve(scriptDirectory, '..');
const frontendDirectory = path.join(rootDirectory, 'frontend');
const backendProjectPath = path.join(rootDirectory, 'backend', 'src', 'BilliardSystem.API', 'BilliardSystem.API.csproj');
const envFilePath = path.join(rootDirectory, '.env');
const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm';
const dockerCommand = process.platform === 'win32' ? 'docker.exe' : 'docker';

function parseDotEnv(filePath) {
  const values = {};
  for (const rawLine of readFileSync(filePath, 'utf8').split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq < 0) continue;
    const key = line.slice(0, eq).trim();
    let value = line.slice(eq + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    values[key] = value;
  }
  return values;
}

// Single source of truth: .env (same credentials the Docker DB uses).
// The backend auto-applies EF migrations at startup, so no `dotnet ef update` step.
function backendEnv() {
  if (!existsSync(envFilePath)) {
    console.error('[setup] missing .env at the repo root. Copy .env.example to .env and fill POSTGRES_PASSWORD first.');
    process.exit(1);
  }
  const dotEnv = parseDotEnv(envFilePath);
  const pgPassword = dotEnv.POSTGRES_PASSWORD;
  if (!pgPassword || pgPassword.startsWith('change-me')) {
    console.error('[setup] .env has no usable POSTGRES_PASSWORD. Fill it in before running the API natively.');
    process.exit(1);
  }
  const env = {
    ASPNETCORE_ENVIRONMENT: 'Development',
    AllowedHosts: 'localhost;127.0.0.1',
    ConnectionStrings__BilliardDatabase: `Host=127.0.0.1;Port=5433;Database=billiard;Username=postgres;Password=${pgPassword};SslMode=Disable`
  };
  if (dotEnv.JWT_KEY && !dotEnv.JWT_KEY.startsWith('change-me')) {
    env.Jwt__Key = dotEnv.JWT_KEY;
  }
  if (dotEnv.SUPER_USERNAME) {
    env.Super__UserName = dotEnv.SUPER_USERNAME;
  }
  if (dotEnv.SUPER_PASSWORD && !dotEnv.SUPER_PASSWORD.startsWith('CHANGE-ME')) {
    env.Super__Password = dotEnv.SUPER_PASSWORD;
  }
  return env;
}

function ensureDbUp() {
  const result = spawnSync(
    dockerCommand,
    ['compose', '-f', 'docker-compose.yml', '-f', 'docker-compose.local.yml', 'up', '-d', 'db'],
    { cwd: rootDirectory, stdio: 'inherit', shell: process.platform === 'win32' }
  );

  if (result.error ?? result.status !== 0) {
    console.error('[db] could not start the Postgres container. Run `npm run db:up` manually.');
    process.exit(1);
  }
}

function createCommand(command, args, options = {}) {
  return {
    command,
    args,
    cwd: options.cwd ?? rootDirectory,
    env: options.env ?? {},
    label: options.label ?? command
  };
}

function buildCommands(selectedMode, dotnetEnv) {
  if (selectedMode === 'ui') {
    return [
      createCommand(npmCommand, ['--prefix', frontendDirectory, 'run', 'start', ...extraArgs], { label: 'ui' })
    ];
  }

  if (selectedMode === 'api') {
    return [
      createCommand('dotnet', ['run', '--project', backendProjectPath, ...extraArgs], {
        label: 'api',
        env: dotnetEnv
      })
    ];
  }

  if (selectedMode === 'all') {
    return [
      createCommand(npmCommand, ['--prefix', frontendDirectory, 'run', 'start', ...extraArgs], { label: 'ui' }),
      createCommand('dotnet', ['run', '--project', backendProjectPath, ...extraArgs], {
        label: 'api',
        env: dotnetEnv
      })
    ];
  }

  return null;
}

function writeOutput(label, chunk, stream = process.stdout) {
  const text = chunk.toString();
  const prefix = `[${label}] `;
  const formatted = text
    .split(/\r?\n/)
    .map((line, index, lines) => {
      if (!line && index === lines.length - 1) {
        return '';
      }

      return `${prefix}${line}`;
    })
    .join('\n');

  stream.write(formatted);
}

function runSingle(commandConfig) {
  return new Promise((resolve, reject) => {
    const child = spawn(commandConfig.command, commandConfig.args, {
      cwd: commandConfig.cwd,
      env: { ...process.env, ...commandConfig.env },
      stdio: ['inherit', 'pipe', 'pipe'],
      shell: false
    });

    child.stdout.on('data', chunk => writeOutput(commandConfig.label, chunk));
    child.stderr.on('data', chunk => writeOutput(commandConfig.label, chunk, process.stderr));

    child.on('error', reject);
    child.on('exit', (code, signal) => {
      if (signal) {
        reject(new Error(`${commandConfig.label} exited with signal ${signal}`));
        return;
      }

      if (code !== 0) {
        reject(new Error(`${commandConfig.label} exited with code ${code}`));
        return;
      }

      resolve();
    });
  });
}

function runCombined(commands) {
  const children = [];
  let resolved = false;

  const shutdown = signal => {
    for (const child of children) {
      if (!child.killed) {
        child.kill(signal);
      }
    }
  };

  const onSignal = signal => {
    shutdown(signal);
    process.exit(0);
  };

  process.on('SIGINT', onSignal);
  process.on('SIGTERM', onSignal);

  return new Promise((resolve, reject) => {
    let finished = 0;

    const finalize = error => {
      if (resolved) {
        return;
      }

      resolved = true;
      process.off('SIGINT', onSignal);
      process.off('SIGTERM', onSignal);
      shutdown();

      if (error) {
        reject(error);
        return;
      }

      resolve();
    };

    for (const commandConfig of commands) {
      const child = spawn(commandConfig.command, commandConfig.args, {
        cwd: commandConfig.cwd,
        env: { ...process.env, ...commandConfig.env },
        stdio: ['inherit', 'pipe', 'pipe'],
        shell: false
      });

      children.push(child);

      child.stdout.on('data', chunk => writeOutput(commandConfig.label, chunk));
      child.stderr.on('data', chunk => writeOutput(commandConfig.label, chunk, process.stderr));

      child.on('error', error => finalize(error));
      child.on('exit', (code, signal) => {
        if (signal) {
          finalize(new Error(`${commandConfig.label} exited with signal ${signal}`));
          return;
        }

        if (code !== 0) {
          finalize(new Error(`${commandConfig.label} exited with code ${code}`));
          return;
        }

        finished += 1;
        if (finished === commands.length) {
          finalize();
        }
      });
    }
  });
}

if (mode === 'migrate') {
  ensureDbUp();
  console.log('[db] Billard applies EF migrations automatically at startup (DatabaseInitializer). DB is up — restart the API or run `npm run dev:api`.');
  process.exit(0);
}

const dotnetEnv = mode === 'ui' ? {} : null;

const commands = buildCommands(mode, dotnetEnv);

if (!commands) {
  console.error('Usage: node scripts/run-billard.mjs <all|ui|api|migrate> [extra args]');
  process.exit(1);
}

if (mode !== 'ui') {
  const env = backendEnv();
  for (const commandConfig of commands) {
    if (commandConfig.label !== 'ui') {
      commandConfig.env = { ...commandConfig.env, ...env };
    }
  }
  ensureDbUp();
}

if (commands.length === 1) {
  await runSingle(commands[0]);
} else {
  await runCombined(commands);
}
