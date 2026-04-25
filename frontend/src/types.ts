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

export type UserCardStats = {
  attack?: number | null;
  defense?: number | null;
  hp?: number | null;
  cost?: number | null;
  level?: number | null;
  colors: string[];
  attribute: string;
};

export type UserCard = {
  id?: string;
  userId: string;
  gameType: DeckGameType;
  externalCardId: string;
  name: string;
  imageUrl: string;
  setName: string;
  rarity: string;
  typeLine: string;
  searchTags: string[];
  mainText: string;
  stats: UserCardStats;
  quantityOwned: number;
  quantityInDecks: number;
  addedAtUtc: string;
  updatedAtUtc: string;
};

export type UserCardsByFieldStat = {
  label: string;
  totalOwnedCopies: number;
};

export type UserCardsTopCardStat = {
  externalCardId: string;
  name: string;
  gameType: string;
  totalOwnedCopies: number;
};

export type UserCardsStats = {
  totalUniqueCards: number;
  totalOwnedCopies: number;
  distinctSets: number;
  averageCopiesPerCard: number;
  gameTypeDistribution: UserCardsByFieldStat[];
  rarityDistribution: UserCardsByFieldStat[];
  setDistribution: UserCardsByFieldStat[];
  topCards: UserCardsTopCardStat[];
};