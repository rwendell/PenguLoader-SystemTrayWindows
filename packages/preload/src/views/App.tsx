import { Toaster } from './components/Toaster';
import { CommandBar } from './components/CommandBar';
import { Welcome } from './components/Welcome';
import { UpdateBanner } from './components/UpdateBanner';
import { SettingsDrawer } from './components/Settings';

export default function App() {
  return (
    <>
      <Welcome />
      <CommandBar />
      <SettingsDrawer />
      <UpdateBanner />
      <Toaster
        gutter={8}
        position="bottom-right"
      />
    </>
  )
}