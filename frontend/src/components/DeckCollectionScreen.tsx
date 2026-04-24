import { FormEvent, useEffect, useRef, useState } from 'react';
import { createDeck, getDecks } from '../api/client';
import { DECK_GAME_TYPE_OPTIONS, getDeckGameTypeLabel } from '../types';
import type { AuthUser, Deck, DeckGameType } from '../types';
import { DeckDetailScreen } from './DeckDetailScreen';
import { FiCheck, FiChevronDown, FiPlus, FiUser, FiX } from 'react-icons/fi';

type DeckCollectionScreenProps = {
  currentUser: AuthUser;
  onLogout: () => void;
};

export function DeckCollectionScreen({ currentUser, onLogout }: DeckCollectionScreenProps) {
  const [decks, setDecks] = useState<Deck[]>([]);
  const [isDecksLoading, setIsDecksLoading] = useState(false);
  const [deckErrorMessage, setDeckErrorMessage] = useState('');
  const [showCreateDeckForm, setShowCreateDeckForm] = useState(false);
  const [newDeckName, setNewDeckName] = useState('');
  const [newDeckDescription, setNewDeckDescription] = useState('');
  const [newDeckGameType, setNewDeckGameType] = useState<DeckGameType>('pokemon');
  const [isGameTypeMenuOpen, setIsGameTypeMenuOpen] = useState(false);
  const [isCreatingDeck, setIsCreatingDeck] = useState(false);
  const [openedDeckId, setOpenedDeckId] = useState<string | null>(null);
  const gameTypeMenuRef = useRef<HTMLDivElement | null>(null);

  const userId = currentUser.id;
  const userName = currentUser.displayName || currentUser.email;
  const selectedDeck = openedDeckId
    ? decks.find((deck) => deck.id === openedDeckId) ?? null
    : null;

  useEffect(() => {
    if (!userId) {
      setDecks([]);
      setDeckErrorMessage('El usuario actual no tiene id para cargar barajas.');
      return;
    }

    const loadDecks = async () => {
      setIsDecksLoading(true);
      setDeckErrorMessage('');

      try {
        const loadedDecks = await getDecks(userId);
        setDecks(loadedDecks);
        setOpenedDeckId((currentId) => {
          if (currentId && loadedDecks.some((deck) => deck.id === currentId)) {
            return currentId;
          }

          return null;
        });
      } catch (error) {
        if (error instanceof Error) {
          setDeckErrorMessage(error.message);
        } else {
          setDeckErrorMessage('No se pudo conectar con la API para cargar barajas.');
        }
      } finally {
        setIsDecksLoading(false);
      }
    };

    void loadDecks();
  }, [userId]);

  useEffect(() => {
    if (!showCreateDeckForm) {
      setIsGameTypeMenuOpen(false);
      return;
    }

    const handleDocumentMouseDown = (event: MouseEvent) => {
      if (!gameTypeMenuRef.current?.contains(event.target as Node)) {
        setIsGameTypeMenuOpen(false);
      }
    };

    document.addEventListener('mousedown', handleDocumentMouseDown);
    return () => {
      document.removeEventListener('mousedown', handleDocumentMouseDown);
    };
  }, [showCreateDeckForm]);

  const handleCreateDeck = async (event: FormEvent) => {
    event.preventDefault();
    setDeckErrorMessage('');

    if (!userId) {
      setDeckErrorMessage('No hay usuario autenticado para crear la baraja.');
      return;
    }

    const name = newDeckName.trim();
    if (!name) {
      setDeckErrorMessage('El nombre de la baraja es obligatorio.');
      return;
    }

    setIsCreatingDeck(true);

    try {
      const createdDeck = await createDeck({
        userId,
        name,
        description: newDeckDescription.trim(),
        gameType: newDeckGameType,
      });

      setDecks((previousDecks) => [createdDeck, ...previousDecks]);
      setNewDeckName('');
      setNewDeckDescription('');
      setNewDeckGameType('pokemon');
      setShowCreateDeckForm(false);
    } catch (error) {
      if (error instanceof Error) {
        setDeckErrorMessage(error.message);
      } else {
        setDeckErrorMessage('No se pudo conectar con la API para crear la baraja.');
      }
    } finally {
      setIsCreatingDeck(false);
    }
  };

  const openDeckDetail = (deckId: string | null) => {
    setOpenedDeckId(deckId);
  };

  const closeCreateDeckModal = () => {
    setShowCreateDeckForm(false);
    setIsGameTypeMenuOpen(false);
    setDeckErrorMessage('');
  };

  const selectedGameTypeLabel = DECK_GAME_TYPE_OPTIONS.find((option) => option.value === newDeckGameType)?.label ?? 'Selecciona un tipo';

  const renderDeckCollection = () => (
    <section className="deck-page-card">
      <header className="deck-header">
        <div>
          <p className="eyebrow">DeckBuilder</p>
          <h1 className="deck-title">Coleccion de barajas</h1>
          <p className="hero-copy">Bienvenido, {userName}.</p>
        </div>

        <button
          type="button"
          className="primary-button icon-label-button"
          onClick={() => {
            setShowCreateDeckForm(true);
            setDeckErrorMessage('');
          }}
        >
          <FiPlus aria-hidden="true" />
          Nueva baraja
        </button>
      </header>

      {deckErrorMessage && !showCreateDeckForm && <p className="error-message">{deckErrorMessage}</p>}

      {isDecksLoading ? (
        <p className="deck-state-text">Cargando barajas...</p>
      ) : decks.length === 0 ? (
        <p className="deck-state-text">No tienes barajas todavia. Crea la primera.</p>
      ) : (
        <ul className="deck-list">
          {decks.map((deck) => (
            <li key={deck.id ?? `${deck.name}-${deck.createdAtUtc}`} className="deck-list-item">
              <div>
                <h2>{deck.name}</h2>
                <p>{deck.description || 'Sin descripcion'}</p>
                <p className="deck-game-type">
                  {getDeckGameTypeLabel(deck.gameType)}
                </p>
              </div>
              <div className="deck-item-actions">
                <span className="deck-meta">
                  {deck.cards.reduce((sum, card) => sum + card.quantity, 0)} cartas
                </span>
                <button
                  type="button"
                  className="secondary-button"
                  onClick={() => {
                    openDeckDetail(deck.id ?? null);
                  }}
                >
                  Abrir baraja
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}

    </section>
  );

  return (
    <main className="app-shell deck-shell">
      <button
        type="button"
        className="logout-button icon-label-button"
        onClick={() => {
          closeCreateDeckModal();
          onLogout();
        }}
      >
        <FiUser aria-hidden="true" />
        Cerrar sesion
      </button>

      {selectedDeck ? (
        <DeckDetailScreen
          deck={selectedDeck}
          userId={userId ?? ''}
          onBackToCollection={() => {
            openDeckDetail(null);
          }}
          onDeckUpdated={(updatedDeck) => {
            setDecks((currentDecks) => currentDecks.map((deck) => (deck.id === updatedDeck.id ? updatedDeck : deck)));
          }}
            onDeckDeleted={(deletedDeckId) => {
              setDecks((currentDecks) => currentDecks.filter((deck) => deck.id !== deletedDeckId));
              openDeckDetail(null);
            }}
        />
      ) : renderDeckCollection()}

      {!selectedDeck && showCreateDeckForm && (
        <div className="create-deck-modal-backdrop" onClick={closeCreateDeckModal}>
          <section
            className="create-deck-modal"
            onClick={(event) => {
              event.stopPropagation();
            }}
            aria-modal="true"
            role="dialog"
            aria-labelledby="create-deck-title"
          >
            <header className="create-deck-modal-header">
              <h2 id="create-deck-title">Nueva baraja</h2>
              <button
                type="button"
                className="secondary-button icon-label-button create-deck-modal-close-button"
                onClick={closeCreateDeckModal}
                aria-label="Cerrar modal"
              >
                <FiX aria-hidden="true" />
                Cerrar
              </button>
            </header>

            <form className="create-deck-form" onSubmit={handleCreateDeck}>
              <label className="field">
                <span>Nombre de la baraja</span>
                <input
                  type="text"
                  placeholder="Ej: Mazo agresivo"
                  value={newDeckName}
                  onChange={(event) => setNewDeckName(event.target.value)}
                  required
                />
              </label>

              <label className="field">
                <span>Descripcion (opcional)</span>
                <input
                  type="text"
                  placeholder="Plan de juego o notas"
                  value={newDeckDescription}
                  onChange={(event) => setNewDeckDescription(event.target.value)}
                />
              </label>

              <label className="field">
                <span>Tipo de baraja</span>
                <div className="game-type-select" ref={gameTypeMenuRef}>
                  <button
                    type="button"
                    className="game-type-trigger"
                    onClick={() => {
                      setIsGameTypeMenuOpen((currentValue) => !currentValue);
                    }}
                    aria-haspopup="listbox"
                    aria-expanded={isGameTypeMenuOpen}
                    aria-label="Seleccionar tipo de baraja"
                  >
                    <span>{selectedGameTypeLabel}</span>
                    <FiChevronDown aria-hidden="true" />
                  </button>

                  {isGameTypeMenuOpen && (
                    <ul className="game-type-menu" role="listbox" aria-label="Tipos de baraja">
                      {DECK_GAME_TYPE_OPTIONS.map((option) => {
                        const isSelected = option.value === newDeckGameType;

                        return (
                          <li key={option.value}>
                            <button
                              type="button"
                              role="option"
                              aria-selected={isSelected}
                              className={`game-type-option${isSelected ? ' is-active' : ''}`}
                              onClick={() => {
                                setNewDeckGameType(option.value);
                                setIsGameTypeMenuOpen(false);
                              }}
                            >
                              <span>{option.label}</span>
                              {isSelected && <FiCheck aria-hidden="true" />}
                            </button>
                          </li>
                        );
                      })}
                    </ul>
                  )}
                </div>
              </label>

              <button type="submit" className="primary-button" disabled={isCreatingDeck}>
                {isCreatingDeck ? 'Creando...' : 'Crear baraja'}
              </button>

              {deckErrorMessage && <p className="error-message">{deckErrorMessage}</p>}
            </form>
          </section>
        </div>
      )}
    </main>
  );
}