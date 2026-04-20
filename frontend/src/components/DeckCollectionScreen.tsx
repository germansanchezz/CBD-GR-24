import { FormEvent, useEffect, useState } from 'react';
import { createDeck, getDecks } from '../api/client';
import type { AuthUser, Deck } from '../types';

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
  const [isCreatingDeck, setIsCreatingDeck] = useState(false);

  const userId = currentUser.id;
  const userName = currentUser.displayName || currentUser.email;

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
      });

      setDecks((previousDecks) => [createdDeck, ...previousDecks]);
      setNewDeckName('');
      setNewDeckDescription('');
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

  return (
    <main className="app-shell deck-shell">
      <button
        type="button"
        className="logout-button"
        onClick={() => {
          setShowCreateDeckForm(false);
          setDeckErrorMessage('');
          onLogout();
        }}
      >
        Cerrar sesion
      </button>

      <section className="deck-page-card">
        <header className="deck-header">
          <div>
            <p className="eyebrow">Pokemon Deck Builder</p>
            <h1 className="deck-title">Coleccion de barajas</h1>
            <p className="hero-copy">Bienvenido, {userName}.</p>
          </div>

          <button
            type="button"
            className="primary-button"
            onClick={() => {
              setShowCreateDeckForm((currentValue) => !currentValue);
              setDeckErrorMessage('');
            }}
          >
            {showCreateDeckForm ? 'Cancelar' : 'Nueva baraja'}
          </button>
        </header>

        {showCreateDeckForm && (
          <form className="create-deck-form" onSubmit={handleCreateDeck}>
            <label className="field">
              <span>Nombre de la baraja</span>
              <input
                type="text"
                placeholder="Ej: Pikachu agresivo"
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

            <button type="submit" className="primary-button" disabled={isCreatingDeck}>
              {isCreatingDeck ? 'Creando...' : 'Crear baraja'}
            </button>
          </form>
        )}

        {deckErrorMessage && <p className="error-message">{deckErrorMessage}</p>}

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
                </div>
                <span className="deck-meta">{deck.cards.length} cartas</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}