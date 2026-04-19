import { FormEvent, useState } from 'react';

type AuthMode = 'login' | 'register';

type AuthUser = {
  id?: string;
  email: string;
  displayName: string;
};

const API_BASE_URL = 'http://localhost:5000';

export default function App() {
  const [mode, setMode] = useState<AuthMode>('login');
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [currentUser, setCurrentUser] = useState<AuthUser | null>(null);

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

  if (currentUser) {
    return (
      <main className="app-shell deck-shell">
        <button type="button" className="logout-button" onClick={() => setCurrentUser(null)}>
          Cerrar sesion
        </button>

        <section className="hero-card hero-card-center">
          <p className="eyebrow">Pokemon Deck Builder</p>
          <h1>Barajas</h1>
          <p className="hero-copy">Bienvenido, {currentUser.displayName || currentUser.email}.</p>
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
