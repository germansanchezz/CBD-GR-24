import { FormEvent, useState } from 'react';
import { authenticateUser } from '../api/client';
import type { AuthMode, AuthUser } from '../types';

type AuthScreenProps = {
  onAuthenticated: (user: AuthUser) => void;
};

export function AuthScreen({ onAuthenticated }: AuthScreenProps) {
  const [mode, setMode] = useState<AuthMode>('login');
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setErrorMessage('');
    setIsSubmitting(true);

    try {
      const user = await authenticateUser({
        mode,
        email,
        password,
        displayName,
      });

      onAuthenticated(user);
      setPassword('');
    } catch (error) {
      if (error instanceof Error) {
        setErrorMessage(error.message);
      } else {
        setErrorMessage('No se pudo conectar con la API.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="app-shell auth-shell">
      <section className="hero-card">
        <p className="eyebrow">DeckBuilder</p>
        <h1>{mode === 'login' ? 'Login' : 'Registro'}</h1>
        <p className="hero-copy">Acceso simple para gestionar tus barajas multijuego.</p>
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