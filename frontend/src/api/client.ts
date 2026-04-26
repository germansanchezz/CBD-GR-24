import type {
  AuthMode,
  AuthUser,
  Deck,
  DeckCard,
  DeckGameType,
  TcgSearchCard,
  UserCard,
  UserCardsStats,
} from '../types';

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

export async function deleteMyAccount(userId: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/auth/me`, {
    method: 'DELETE',
    headers: {
      'X-User-Id': userId,
    },
  });

  if (!response.ok) {
    const message = await readApiErrorMessage(response, 'No se pudo eliminar la cuenta.');
    throw new Error(message);
  }
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

export async function deleteDeck(args: {
  userId: string;
  deckId: string;
}): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/decks/${args.deckId}`, {
    method: 'DELETE',
    headers: {
      'X-User-Id': args.userId,
    },
  });

  if (!response.ok) {
    const message = await readApiErrorMessage(response, 'No se pudo eliminar la baraja.');
    throw new Error(message);
  }
}

export async function searchCardsByGameType(args: {
  gameType: DeckGameType;
  name: string;
}): Promise<TcgSearchCard[]> {
  const query = args.name.trim();
  if (!query) {
    return [];
  }

  const response = await fetch(
    `${API_BASE_URL}/api/cards/search?gameType=${args.gameType}&name=${encodeURIComponent(query)}`,
  );

  if (!response.ok) {
    const message = await readApiErrorMessage(response, 'No se pudo buscar cartas para esta baraja.');
    throw new Error(message);
  }

  return await response.json() as TcgSearchCard[];
}

export async function saveUserCardFromSearchResult(args: {
  userId: string;
  gameType: DeckGameType;
  card: TcgSearchCard;
  quantityOwned?: number;
}): Promise<UserCard> {
  const response = await fetch(`${API_BASE_URL}/api/user-cards`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-User-Id': args.userId,
    },
    body: JSON.stringify({
      gameType: args.gameType,
      externalCardId: args.card.cardId,
      name: args.card.name,
      imageUrl: args.card.imageUrl,
      setName: '',
      rarity: '',
      typeLine: '',
      searchTags: [],
      mainText: '',
      quantityOwned: args.quantityOwned ?? 1,
    }),
  });

  if (!response.ok) {
    const message = await readApiErrorMessage(response, 'No se pudo guardar la carta en la coleccion.');
    throw new Error(message);
  }

  return await response.json() as UserCard;
}

export async function getUserCardsStats(args: {
  userId: string;
  gameType?: DeckGameType;
}): Promise<UserCardsStats> {
  const query = args.gameType ? `?gameType=${args.gameType}` : '';

  const response = await fetch(`${API_BASE_URL}/api/user-cards/stats${query}`, {
    headers: {
      'X-User-Id': args.userId,
    },
  });

  if (!response.ok) {
    const message = await readApiErrorMessage(response, 'No se pudieron cargar las estadisticas de la coleccion.');
    throw new Error(message);
  }

  return await response.json() as UserCardsStats;
}
