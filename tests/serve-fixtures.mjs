import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));
const stream = 'BT /F1 16 Tf 54 730 Td (Selection Probe - PDF fixture) Tj 0 -42 Td /F1 12 Tf (A faithful translation begins with accurate source text.) Tj 0 -23 Td (Keep numbers 12345, punctuation, and technical terms unchanged.) Tj 0 -40 Td (This paragraph contains a visual line break) Tj 0 -20 Td (that can be compared with the original selection.) Tj ET';
const objects = ['<< /Type /Catalog /Pages 2 0 R >>','<< /Type /Pages /Kids [3 0 R] /Count 1 >>','<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>','<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',`<< /Length ${Buffer.byteLength(stream)} >>\nstream\n${stream}\nendstream`];
let pdf = '%PDF-1.4\n';
const offsets = [0];
objects.forEach((object, index) => { offsets.push(Buffer.byteLength(pdf)); pdf += `${index+1} 0 obj\n${object}\nendobj\n`; });
const xref = Buffer.byteLength(pdf);
pdf += `xref\n0 6\n0000000000 65535 f \n${offsets.slice(1).map(offset => String(offset).padStart(10, '0') + ' 00000 n \n').join('')}trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
http.createServer((request, response) => {
  if (request.url === '/fixture.pdf') { response.writeHead(200, { 'Content-Type': 'application/pdf' }); response.end(pdf); }
  else if (request.url === '/') { response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' }); response.end(fs.readFileSync(path.join(here,'fixture.html'))); }
  else { response.writeHead(404); response.end('Not found'); }
}).listen(18764, '127.0.0.1', () => console.log('Selection fixtures: http://127.0.0.1:18764'));
