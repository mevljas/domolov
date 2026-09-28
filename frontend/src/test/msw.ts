import { setupServer } from 'msw/node'

/** Shared msw server; tests add handlers with `server.use(...)`. */
export const server = setupServer()
