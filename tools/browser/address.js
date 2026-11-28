// The address a script opens the game at. A new saga in the browser rolls its own seed, so a script asks for one in the address
// (seed 1, or PRAETORIA_SEED) and plays the same world on every run. PRAETORIA_URL overrides the address; a seed already in it wins.
module.exports = (query = '') => {
  const u = new URL(process.env.PRAETORIA_URL || 'http://localhost:8112/');
  for (const [k, v] of new URLSearchParams(query.replace(/^\?/, ''))) u.searchParams.set(k, v);
  if (!u.searchParams.has('seed')) u.searchParams.set('seed', process.env.PRAETORIA_SEED || '1');
  return u.toString();
};
