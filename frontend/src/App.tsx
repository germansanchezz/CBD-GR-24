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
      <main style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', position: 'relative' }}>
        <button
          type="button"
          onClick={() => setCurrentUser(null)}
          style={{ position: 'absolute', top: '16px', right: '16px' }}
        >
          Cerrar sesion
        </button>
        <h1>Barajas</h1>
      </main>
    );
  }

  return (
    <main style={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}>
      <form onSubmit={handleSubmit} style={{ display: 'grid', gap: '12px', minWidth: '280px' }}>
        <h1>{mode === 'login' ? 'Login' : 'Registro'}</h1>

        {mode === 'register' && (
          <input
            type="text"
            placeholder="Nombre"
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
            required
          />
        )}

        <input
          type="email"
          placeholder="Email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          required
        />

        <input
          type="password"
          placeholder="Password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          required
        />

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Enviando...' : mode === 'login' ? 'Entrar' : 'Registrarme'}
        </button>

        <button
          type="button"
          onClick={() => {
            setMode(mode === 'login' ? 'register' : 'login');
            setErrorMessage('');
          }}
        >
          {mode === 'login' ? 'Ir a registro' : 'Ir a login'}
        </button>

        {errorMessage && <p>{errorMessage}</p>}
      </form>
    </main>
  );
}
