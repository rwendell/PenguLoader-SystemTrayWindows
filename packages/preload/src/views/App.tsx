import { Toaster } from './components/Toaster';
import { CommandBar } from './components/CommandBar';
import { Welcome } from './components/Welcome';
import { UpdateBanner } from './components/UpdateBanner';

export default function App() {
  return (
    <div>
      <Welcome />
      <CommandBar />
      <UpdateBanner />
      <Toaster
        gutter={8}
        position="bottom-right"
      />
    </div>
  )
}