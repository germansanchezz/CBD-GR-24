import { FormEvent, useEffect, useState } from 'react';

type AuthMode = 'login' | 'register';

type AuthUser = {
  id?: string;
  email: string;
  displayName: string;
};

type Deck = {
  id?: string;
  name: string;
  description: string;
  cards: Array<unknown>;
  createdAtUtc: string;
  updatedAtUtc: string;
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';

export default function App() {
  const [mode, setMode] = useState<AuthMode>('login');
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [currentUser, setCurrentUser] = useState<AuthUser | null>(null);
  const [decks, setDecks] = useState<Deck[]>([]);
  const [isDecksLoading, setIsDecksLoading] = useState(false);
  const [deckErrorMessage, setDeckErrorMessage] = useState('');
  const [showCreateDeckForm, setShowCreateDeckForm] = useState(false);
  const [newDeckName, setNewDeckName] = useState('');
  const [newDeckDescription, setNewDeckDescription] = useState('');
  const [isCreatingDeck, setIsCreatingDeck] = useState(false);

  const fetchDecks = async (userId: string) => {
    setIsDecksLoading(true);
    setDeckErrorMessage('');

    try {
      const response = await fetch(`${API_BASE_URL}/api/decks`, {
        headers: {
          'X-User-Id': userId,
        },
      });

      if (!response.ok) {
        const payload = await response.json().catch(() => null) as { message?: string } | null;
        setDeckErrorMessage(payload?.message ?? 'No se pudieron cargar las barajas.');
        return;
      }

      const data = await response.json() as Deck[];
      setDecks(data);
    } catch {
      setDeckErrorMessage('No se pudo conectar con la API para cargar barajas.');
    } finally {
      setIsDecksLoading(false);
    }
  };

  useEffect(() => {
    if (!currentUser?.id) {
      setDecks([]);
      return;
    }

    void fetchDecks(currentUser.id);
  }, [currentUser?.id]);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setErrorMessage('');
    setIsSubmitting(true);

    try {
      const endpoint = mode === 'login' ? '/api/auth/login' : '/api/auth/register';
      const body = mode === 'login'
        ? { email, password }
        : { email, password, displayName };

      const response = await fetch(`${API_BASE_URL}${endpoint}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(body),
      });

      if (!response.ok) {
        if (response.status === 401) {
          setErrorMessage('Credenciales incorrectas.');
          return;
        }

        const payload = await response.json().catch(() => null) as { message?: string } | null;
        setErrorMessage(payload?.message ?? 'No se pudo completar la solicitud.');
        return;
      }

      const user = await response.json() as AuthUser;
      setCurrentUser(user);
      setPassword('');
    } catch {
      setErrorMessage('No se pudo conectar con la API.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCreateDeck = async (event: FormEvent) => {
    event.preventDefault();
    setDeckErrorMessage('');

    if (!currentUser?.id) {
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
      const response = await fetch(`${API_BASE_URL}/api/decks`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-User-Id': currentUser.id,
        },
        body: JSON.stringify({
          name,
          description: newDeckDescription.trim(),
        }),
      });

      if (!response.ok) {
        const payload = await response.json().catch(() => null) as { message?: string } | null;
        setDeckErrorMessage(payload?.message ?? 'No se pudo crear la baraja.');
        return;
      }

      const createdDeck = await response.json() as Deck;
      setDecks((previousDecks) => [createdDeck, ...previousDecks]);
      setNewDeckName('');
      setNewDeckDescription('');
      setShowCreateDeckForm(false);
    } catch {
      setDeckErrorMessage('No se pudo conectar con la API para crear la baraja.');
    } finally {
      setIsCreatingDeck(false);
    }
  };

  if (currentUser) {
    const userName = currentUser.displayName || currentUser.email;

    return (
      <main className="app-shell deck-shell">
        <button
          type="button"
          className="logout-button"
          onClick={() => {
            setCurrentUser(null);
            setShowCreateDeckForm(false);
            setDeckErrorMessage('');
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

  return (
    <main className="app-shell auth-shell">
      <section className="hero-card">
        <p className="eyebrow">Pokemon TCG</p>
        <h1>{mode === 'login' ? 'Login' : 'Registro'}</h1>
        <p className="hero-copy">Acceso simple para entrar en la pantalla de barajas.</p>
      </section>

      <form className="auth-card" onSubmit={handleSubmit}>
        {mode === 'register' && (
          <label className="field">
            <span>Nombre</span>
            <input
              type="text"
              placeholder="Tu nombre"
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              required
            />
          </label>
        )}

        <label className="field">
          <span>Email</span>
          <input
            type="email"
            placeholder="tu@email.com"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
          />
        </label>

        <label className="field">
          <span>Password</span>
          <input
            type="password"
            placeholder="Password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
          />
        </label>

        <button type="submit" className="primary-button" disabled={isSubmitting}>
          {isSubmitting ? 'Enviando...' : mode === 'login' ? 'Entrar' : 'Registrarme'}
        </button>

        <button
          type="button"
          className="secondary-button"
          onClick={() => {
            setMode(mode === 'login' ? 'register' : 'login');
            setErrorMessage('');
          }}
        >
          {mode === 'login' ? 'Ir a registro' : 'Ir a login'}
        </button>

        {errorMessage && <p className="error-message">{errorMessage}</p>}
      </form>
    </main>
  );
}
