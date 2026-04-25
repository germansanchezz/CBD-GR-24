import { FormEvent, useState } from 'react';
import { deleteDeck, saveUserCardFromSearchResult, searchCardsByGameType, updateDeck } from '../api/client';
import { getDeckGameTypeLabel } from '../types';
import type { Deck, DeckCard, TcgSearchCard } from '../types';
import { FiPlus, FiMinus, FiSearch, FiX, FiArchive } from 'react-icons/fi';

type DeckDetailScreenProps = {
  deck: Deck;
  userId: string;
  onBackToCollection: () => void;
  onDeckUpdated: (updatedDeck: Deck) => void;
  onDeckDeleted: (deckId: string) => void;
};

export function DeckDetailScreen({
  deck,
  userId,
  onBackToCollection,
  onDeckUpdated,
  onDeckDeleted,
}: DeckDetailScreenProps) {
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [searchText, setSearchText] = useState('');
  const [searchResults, setSearchResults] = useState<TcgSearchCard[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [isSavingCards, setIsSavingCards] = useState(false);
  const [isSavingToCollection, setIsSavingToCollection] = useState(false);
  const [editorMessage, setEditorMessage] = useState('');

  const getTotalCards = (cards: DeckCard[]) => cards.reduce((sum, card) => sum + card.quantity, 0);

  const getCopiesByName = (cards: DeckCard[], name: string) => cards
    .filter((card) => card.name.toLowerCase() === name.toLowerCase())
    .reduce((sum, card) => sum + card.quantity, 0);

  const getQuantityInDeck = (cardId: string) => deck.cards.find((card) => card.cardId === cardId)?.quantity ?? 0;
  const maxCopiesPerName = deck.gameType === 'yugioh' ? 3 : 4;

  const persistCards = async (nextCards: DeckCard[]) => {
    if (!deck.id) {
      setEditorMessage('No se pudo guardar: baraja sin id.');
      return;
    }

    setIsSavingCards(true);
    setEditorMessage('');

    try {
      const updatedDeck = await updateDeck({
        userId,
        deckId: deck.id,
        name: deck.name,
        description: deck.description,
        cards: nextCards,
      });

      onDeckUpdated(updatedDeck);
    } catch (error) {
      if (error instanceof Error) {
        setEditorMessage(error.message);
      } else {
        setEditorMessage('No se pudo guardar la baraja.');
      }
    } finally {
      setIsSavingCards(false);
    }
  };

  const handleDeleteDeck = async () => {
    if (!deck.id) {
      setEditorMessage('No se pudo eliminar: baraja sin id.');
      return;
    }

    const confirmed = window.confirm(`¿Eliminar la baraja "${deck.name}"? Esta accion no se puede deshacer.`);
    if (!confirmed) {
      return;
    }

    setEditorMessage('');

    try {
      await deleteDeck({
        userId,
        deckId: deck.id,
      });
      onDeckDeleted(deck.id);
    } catch (error) {
      if (error instanceof Error) {
        setEditorMessage(error.message);
      } else {
        setEditorMessage('No se pudo eliminar la baraja.');
      }
    }
  };

  const updateCardQuantity = async (card: TcgSearchCard | DeckCard, delta: 1 | -1) => {
    const existingCards = deck.cards;
    const cardIndex = existingCards.findIndex((existingCard) => existingCard.cardId === card.cardId);
    const nextCards = existingCards.map((existingCard) => ({ ...existingCard }));
    if (delta === 1) {
      const totalCards = getTotalCards(existingCards);
      const sameNameCopies = getCopiesByName(existingCards, card.name);

      if (totalCards >= 60) {
        setEditorMessage('La baraja ya tiene 60 cartas.');
        return;
      }

      if (sameNameCopies >= maxCopiesPerName) {
        setEditorMessage(`No puedes tener mas de ${maxCopiesPerName} copias con el mismo nombre.`);
        return;
      }

      if (cardIndex >= 0) {
        nextCards[cardIndex].quantity += 1;
      } else {
        nextCards.push({
          cardId: card.cardId,
          name: card.name,
          imageUrl: card.imageUrl,
          quantity: 1,
        });
      }
    }

    if (delta === -1) {
      if (cardIndex < 0) {
        return;
      }

      if (nextCards[cardIndex].quantity <= 1) {
        nextCards.splice(cardIndex, 1);
      } else {
        nextCards[cardIndex].quantity -= 1;
      }
    }

    await persistCards(nextCards);
  };

  const handleSearchSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const query = searchText.trim();
    if (!query) {
      setEditorMessage('Escribe un nombre para buscar cartas.');
      return;
    }

    setIsSearching(true);
    setEditorMessage('');

    try {
      const cards = await searchCardsByGameType({
        gameType: deck.gameType,
        name: query,
      });
      setSearchResults(cards);
    } catch (error) {
      if (error instanceof Error) {
        setEditorMessage(error.message);
      } else {
        setEditorMessage('No se pudo buscar cartas para esta baraja.');
      }
    } finally {
      setIsSearching(false);
    }
  };

  const handleClearSearch = () => {
    setSearchText('');
    setSearchResults([]);
    setEditorMessage('');
  };

  const handleSaveToCollection = async (card: TcgSearchCard) => {
    setIsSavingToCollection(true);
    setEditorMessage('');

    try {
      await saveUserCardFromSearchResult({
        userId,
        gameType: deck.gameType,
        card,
        quantityOwned: 1,
      });
      setEditorMessage(`Carta "${card.name}" guardada en coleccion.`);
    } catch (error) {
      if (error instanceof Error) {
        setEditorMessage(error.message);
      } else {
        setEditorMessage('No se pudo guardar la carta en la coleccion.');
      }
    } finally {
      setIsSavingToCollection(false);
    }
  };

  return (
    <section className="deck-page-card">
      <header className="deck-editor-header">
        <button type="button" className="secondary-button" onClick={onBackToCollection}>
          Volver a coleccion
        </button>

        <button type="button" className="secondary-button danger-button" onClick={() => { void handleDeleteDeck(); }}>
          Eliminar baraja
        </button>

        <button
          type="button"
          className="primary-button"
          onClick={() => {
            setIsSearchOpen((value) => !value);
            setEditorMessage('');
          }}
        >
          {isSearchOpen ? 'Cerrar buscador' : 'Abrir buscador'}
        </button>
      </header>

      <section className="deck-editor">
        <header className="deck-detail-headline">
          <p className="eyebrow">Edicion de baraja</p>
          <h2 className="deck-editor-title">{deck.name}</h2>
          <p className="deck-state-text">
            {getDeckGameTypeLabel(deck.gameType)} | {getTotalCards(deck.cards)} / 60 cartas | Maximo {maxCopiesPerName} copias por nombre
          </p>
        </header>

        {isSearchOpen && (
          <div className="deck-search-panel">
            <form className="deck-search-form" onSubmit={handleSearchSubmit}>
              <label className="field">
                <span>Buscar carta por nombre</span>
                <input
                  type="text"
                  value={searchText}
                  onChange={(event) => setSearchText(event.target.value)}
                  placeholder="Ej: Carta por nombre"
                />
              </label>

              <div className="deck-search-actions">
                <button type="submit" className="primary-button icon-label-button" disabled={isSearching}>
                  <FiSearch aria-hidden="true" />
                  {isSearching ? 'Buscando...' : 'Buscar'}
                </button>

                <button
                  type="button"
                  className="secondary-button icon-label-button"
                  disabled={isSearching || (searchText.trim().length === 0 && searchResults.length === 0)}
                  onClick={handleClearSearch}
                >
                  <FiX aria-hidden="true" />
                  Limpiar
                </button>
              </div>
            </form>

            <div className="card-grid">
              {searchResults.map((card) => (
                <article key={card.cardId} className="card-item">
                  <img src={card.imageUrl} alt={card.name} loading="lazy" />
                  <p>{card.name}</p>
                  <button
                    type="button"
                    className="secondary-button icon-label-button"
                    disabled={isSavingToCollection}
                    onClick={() => {
                      void handleSaveToCollection(card);
                    }}
                  >
                    <FiArchive aria-hidden="true" />
                    Guardar en coleccion
                  </button>
                  <div className="quantity-controls">
                    <button
                      type="button"
                      className="secondary-button quantity-icon-button"
                      disabled={isSavingCards || getQuantityInDeck(card.cardId) === 0}
                      onClick={() => {
                        void updateCardQuantity(card, -1);
                      }}
                      aria-label={`Quitar una copia de ${card.name}`}
                      title="Quitar una copia"
                    >
                      <FiMinus aria-hidden="true" />
                    </button>
                    <span className="quantity-value">{getQuantityInDeck(card.cardId)}</span>
                    <button
                      type="button"
                      className="secondary-button quantity-icon-button"
                      disabled={isSavingCards}
                      onClick={() => {
                        void updateCardQuantity(card, 1);
                      }}
                      aria-label={`Añadir una copia de ${card.name}`}
                      title="Añadir una copia"
                    >
                      <FiPlus aria-hidden="true" />
                    </button>
                  </div>
                </article>
              ))}
            </div>
          </div>
        )}

        <div>
          <h3 className="deck-editor-subtitle">Cartas en la baraja</h3>
          {deck.cards.length === 0 ? (
            <p className="deck-state-text">Todavia no hay cartas en esta baraja.</p>
          ) : (
            <div className="card-grid">
              {deck.cards.map((card) => (
                <article key={card.cardId} className="card-item">
                  <img src={card.imageUrl} alt={card.name} loading="lazy" />
                  <p>{card.name}</p>
                  <div className="quantity-controls">
                    <button
                      type="button"
                      className="secondary-button quantity-icon-button"
                      disabled={isSavingCards}
                      onClick={() => {
                        void updateCardQuantity(card, -1);
                      }}
                      aria-label={`Quitar una copia de ${card.name}`}
                      title="Quitar una copia"
                    >
                      <FiMinus aria-hidden="true" />
                    </button>
                    <span className="quantity-value">{card.quantity}</span>
                    <button
                      type="button"
                      className="secondary-button quantity-icon-button"
                      disabled={isSavingCards}
                      onClick={() => {
                        void updateCardQuantity(card, 1);
                      }}
                      aria-label={`Añadir una copia de ${card.name}`}
                      title="Añadir una copia"
                    >
                      <FiPlus aria-hidden="true" />
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </div>

        {editorMessage && <p className="error-message">{editorMessage}</p>}
      </section>
    </section>
  );
}