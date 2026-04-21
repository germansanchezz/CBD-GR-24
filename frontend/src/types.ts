export type AuthMode = 'login' | 'register';

export type AuthUser = {
  id?: string;
  email: string;
  displayName: string;
};

export type Deck = {
  id?: string;
  name: string;
  description: string;
  cards: DeckCard[];
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type DeckCard = {
  cardId: string;
  name: string;
  imageUrl: string;
  quantity: number;
};

export type TcgSearchCard = {
  cardId: string;
  name: string;
  imageUrl: string;
};