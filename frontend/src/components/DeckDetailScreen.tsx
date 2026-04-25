import { FormEvent, useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { deleteDeck, searchCardsByGameType, updateDeck } from '../api/client';
import { getDeckGameTypeLabel } from '../types';
import type { Deck, DeckCard, TcgSearchCard } from '../types';
import { FiEdit2, FiPlus, FiMinus, FiSearch, FiX } from 'react-icons/fi';

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
  const [deckName, setDeckName] = useState(deck.name);
  const [deckDescription, setDeckDescription] = useState(deck.description);
  const [showEditDeckModal, setShowEditDeckModal] = useState(false);
  const [isSavingDeckMetadata, setIsSavingDeckMetadata] = useState(false);
  const [editDeckErrorMessage, setEditDeckErrorMessage] = useState('');
  const [editorMessage, setEditorMessage] = useState('');

  useEffect(() => {
    setDeckName(deck.name);
    setDeckDescription(deck.description);
  }, [deck.description, deck.id, deck.name]);

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

  const handleSaveDeckMetadata = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = deckName.trim();
    const trimmedDescription = deckDescription.trim();
    const hasMetadataChanges = trimmedName !== deck.name || trimmedDescription !== deck.description;

    if (!hasMetadataChanges) {
      setShowEditDeckModal(false);
      return;
    }

    if (!trimmedName) {
      setEditDeckErrorMessage('El nombre de la baraja es obligatorio.');
      return;
    }

    if (!deck.id) {
      setEditDeckErrorMessage('No se pudo guardar: baraja sin id.');
      return;
    }

    setIsSavingDeckMetadata(true);
    setEditDeckErrorMessage('');

    try {
      const updatedDeck = await updateDeck({
        userId,
        deckId: deck.id,
        name: trimmedName,
        description: trimmedDescription,
        cards: deck.cards,
      });

      onDeckUpdated(updatedDeck);
      setDeckName(updatedDeck.name);
      setDeckDescription(updatedDeck.description);
      setShowEditDeckModal(false);
    } catch (error) {
      if (error instanceof Error) {
        setEditDeckErrorMessage(error.message);
      } else {
        setEditDeckErrorMessage('No se pudo guardar la baraja.');
      }
    } finally {
      setIsSavingDeckMetadata(false);
    }
  };

  const closeEditDeckModal = () => {
    if (isSavingDeckMetadata) {
      return;
    }

    setShowEditDeckModal(false);
    setEditDeckErrorMessage('');
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

  return (
    <>
      <section className="deck-page-card">
        <header className="deck-editor-header">
        <button type="button" className="secondary-button" onClick={onBackToCollection}>
          Volver a colección
        </button>

        <button type="button" className="secondary-button danger-button" onClick={() => { void handleDeleteDeck(); }}>
          Eliminar baraja
        </button>

        <button
          type="button"
          className="secondary-button icon-label-button"
          onClick={() => {
            setDeckName(deck.name);
            setDeckDescription(deck.description);
            setEditDeckErrorMessage('');
            setShowEditDeckModal(true);
          }}
        >
          <FiEdit2 aria-hidden="true" />
          Editar baraja
        </button>

        <button
          type="button"
          className="primary-button icon-label-button"
          onClick={() => {
            setIsSearchOpen((value) => !value);
            setEditorMessage('');
          }}
        >
          {isSearchOpen ? <FiX aria-hidden="true" /> : <FiSearch aria-hidden="true" />}
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
      {showEditDeckModal ? createPortal(
        <div className="create-deck-modal-backdrop" onClick={closeEditDeckModal}>
          <section
            className="create-deck-modal edit-deck-modal"
            onClick={(event) => {
              event.stopPropagation();
            }}
            aria-modal="true"
            role="dialog"
            aria-labelledby="edit-deck-title"
          >
            <header className="create-deck-modal-header">
              <h2 id="edit-deck-title">Editar baraja</h2>
              <button
                type="button"
                className="secondary-button icon-label-button create-deck-modal-close-button"
                onClick={closeEditDeckModal}
                aria-label="Cerrar modal"
                disabled={isSavingDeckMetadata}
              >
                <FiX aria-hidden="true" />
                Cerrar
              </button>
            </header>

            <form className="create-deck-form" onSubmit={handleSaveDeckMetadata}>
              <label className="field">
                <span>Nombre de la baraja</span>
                <input
                  type="text"
                  value={deckName}
                  onChange={(event) => setDeckName(event.target.value)}
                  placeholder="Ej: Mazo control"
                  required
                />
              </label>

              <label className="field">
                <span>Descripcion (opcional)</span>
                <input
                  type="text"
                  value={deckDescription}
                  onChange={(event) => setDeckDescription(event.target.value)}
                  placeholder="Plan de juego o notas"
                />
              </label>

              <button
                type="submit"
                className="primary-button"
                disabled={isSavingDeckMetadata}
              >
                {isSavingDeckMetadata ? 'Guardando...' : 'Guardar cambios'}
              </button>

              {editDeckErrorMessage && <p className="error-message">{editDeckErrorMessage}</p>}
            </form>
          </section>
        </div>,
        document.body,
      ) : null}
    </>
  );
}
