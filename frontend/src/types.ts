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
  cards: Array<unknown>;
  createdAtUtc: string;
  updatedAtUtc: string;
};