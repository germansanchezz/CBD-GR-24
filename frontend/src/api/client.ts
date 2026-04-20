import type { AuthMode, AuthUser, Deck } from '../types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';

type ApiErrorPayload = {
  message?: string;
};

async function readApiErrorMessage(response: Response, fallbackMessage: string): Promise<string> {
  const payload = await response.json().catch(() => null) as ApiErrorPayload | null;
  return payload?.message ?? fallbackMessage;
}

export async function authenticateUser(args: {
  mode: AuthMode;
  email: string;
  password: string;
  displayName: string;
}): Promise<AuthUser> {
  const endpoint = args.mode === 'login' ? '/api/auth/login' : '/api/auth/register';
  const body = args.mode === 'login'
    ? { email: args.email, password: args.password }
    : { email: args.email, password: args.password, displayName: args.displayName };

  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    if (response.status === 401) {
      throw new Error('Credenciales incorrectas.');
    }

    const message = await readApiErrorMessage(response, 'No se pudo completar la solicitud.');
    throw new Error(message);
  }

  return await response.json() as AuthUser;
}

export async function getDecks(userId: string): Promise<Deck[]> {
  const response = await fetch(`${API_BASE_URL}/api/decks`, {
    headers: {
      'X-User-Id': userId,
    },
  });

  if (!response.ok) {
    const message = await readApiErrorMessage(response, 'No se pudieron cargar las barajas.');
    throw new Error(message);
  }

  return await response.json() as Deck[];
}

export async function createDeck(args: {
  userId: string;
  name: string;
  description: string;
}): Promise<Deck> {
  const response = await fetch(`${API_BASE_URL}/api/decks`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-User-Id': args.userId,
    },
    body: JSON.stringify({
      name: args.name,
      description: args.description,
    }),
  });

  if (!response.ok) {
    const message = await readApiErrorMessage(response, 'No se pudo crear la baraja.');
    throw new Error(message);
  }

  return await response.json() as Deck;
}