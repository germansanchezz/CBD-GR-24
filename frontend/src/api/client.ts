import type { AuthMode, AuthUser, Deck, DeckCard, DeckGameType, TcgSearchCard } from '../types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';
const TCGDEX_BASE_URL = 'https://api.tcgdex.net/v2/es';

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
  gameType: DeckGameType;
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
      gameType: args.gameType,
    }),
  });

  if (!response.ok) {
    const message = await readApiErrorMessage(response, 'No se pudo crear la baraja.');
    throw new Error(message);
  }

  return await response.json() as Deck;
}

export async function updateDeck(args: {
  userId: string;
  deckId: string;
  name: string;
  description: string;
  cards: DeckCard[];
}): Promise<Deck> {
  const response = await fetch(`${API_BASE_URL}/api/decks/${args.deckId}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      'X-User-Id': args.userId,
    },
    body: JSON.stringify({
      name: args.name,
      description: args.description,
      cards: args.cards,
    }),
  });

  if (!response.ok) {
    const message = await readApiErrorMessage(response, 'No se pudo guardar la baraja.');
    throw new Error(message);
  }

  return await response.json() as Deck;
}

type TcgSearchRawCard = {
  id: string;
  name: string;
  image?: string;
};

type TcgCardDetail = {
  image?: string;
};

async function resolveCardImageBase(card: TcgSearchRawCard): Promise<string | null> {
  if (card.image) {
    return card.image;
  }

  const response = await fetch(`${TCGDEX_BASE_URL}/cards/${card.id}`);
  if (!response.ok) {
    return null;
  }

  const payload = await response.json() as TcgCardDetail;
  return payload.image ?? null;
}

function buildCardImageUrl(imageBase: string): string {
  return `${imageBase}/high.webp`;
}

export async function searchTcgCardsByName(name: string): Promise<TcgSearchCard[]> {
  const query = name.trim();
  if (!query) {
    return [];
  }

  const response = await fetch(`${TCGDEX_BASE_URL}/cards?name=${encodeURIComponent(query)}`);
  if (!response.ok) {
    throw new Error('No se pudo consultar TCGDex.');
  }

  const rawCards = await response.json() as TcgSearchRawCard[];

  const mappedCards = await Promise.all(rawCards.map(async (card) => {
    const imageBase = await resolveCardImageBase(card);
    if (!imageBase) {
      return null;
    }

    return {
      cardId: card.id,
      name: card.name,
      imageUrl: buildCardImageUrl(imageBase),
    } satisfies TcgSearchCard;
  }));

  return mappedCards.filter((card): card is TcgSearchCard => card !== null);
}