import "./globals.css";
import type { ReactNode } from "react";

export const metadata = {
  title: "Sabemi | Webhooks de Pagamento",
  description: "Painel administrativo de notificações de pagamento do banco parceiro",
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="pt-BR">
      <body>{children}</body>
    </html>
  );
}
