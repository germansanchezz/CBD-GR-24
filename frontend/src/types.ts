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
  gameType: DeckGameType;
  cards: DeckCard[];
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type DeckGameType = 'pokemon' | 'magic' | 'yugioh';

export const DECK_GAME_TYPE_OPTIONS: Array<{ value: DeckGameType; label: string }> = [
  { value: 'pokemon', label: 'Pokémon' },
  { value: 'magic', label: 'Magic: The Gathering' },
  { value: 'yugioh', label: 'Yu-Gi-Oh!' },
];

export function getDeckGameTypeLabel(gameType: DeckGameType): string {
  return DECK_GAME_TYPE_OPTIONS.find((option) => option.value === gameType)?.label ?? gameType;
}

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