import { useState } from 'react';
import { AuthScreen } from './components/AuthScreen';
import { DeckCollectionScreen } from './components/DeckCollectionScreen';
import type { AuthUser } from './types';

export default function App() {
  const [currentUser, setCurrentUser] = useState<AuthUser | null>(null);

  if (currentUser) {
    return <DeckCollectionScreen currentUser={currentUser} onLogout={() => setCurrentUser(null)} />;
  }

  return <AuthScreen onAuthenticated={setCurrentUser} />;
}
