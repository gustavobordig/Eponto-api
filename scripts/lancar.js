// Inicia um script bash a partir do node e repassa os sinais de parada.
// O gerenciador de servidores do app usa este lançador porque, iniciado direto
// por ele, o bash não tem permissão do macOS para ler a pasta Desktop.
const { spawn } = require('child_process');
const filho = spawn('bash', process.argv.slice(2), { stdio: 'inherit' });
for (const sinal of ['SIGINT', 'SIGTERM', 'SIGHUP']) process.on(sinal, () => filho.kill(sinal));
filho.on('exit', (codigo, sinal) => process.exit(codigo ?? (sinal ? 1 : 0)));
