import { createHash } from 'node:crypto';
import { readdirSync, readFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import type { Plugin } from 'vite';

function listFiles(folder: string): string[] {
  return readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    const path = join(folder, entry.name);
    return entry.isDirectory() ? listFiles(path) : [path];
  });
}

/** Składa sw.js z szablonu: wersja to skrót plików, więc nowy build zawsze podmienia pamięć telefonu. */
export function serviceWorker(templatePath: string, publicFolder: string): Plugin {
  return {
    name: 'jajcarz-service-worker',
    apply: 'build',
    generateBundle(_options, bundle) {
      const built = Object.values(bundle).map((output) => `/${output.fileName}`);
      const copied = listFiles(publicFolder).map((path) => `/${relative(publicFolder, path).split(sep).join('/')}`);
      const files = [...new Set([...built, ...copied])].filter((file) => file !== '/sw.js').sort();

      const hash = createHash('sha256');
      for (const output of Object.values(bundle)) {
        hash.update(output.fileName);
        hash.update(output.type === 'chunk' ? output.code : output.source);
      }

      this.emitFile({
        type: 'asset',
        fileName: 'sw.js',
        source: readFileSync(templatePath, 'utf8')
          .replace('__VERSION__', hash.digest('hex').slice(0, 12))
          .replace('__PRECACHE__', JSON.stringify(files)),
      });
    },
  };
}
