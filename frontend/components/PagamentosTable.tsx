"use client";

import { Pagamento } from "@/lib/api";

function formatarMoeda(valor: number) {
  return valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

function formatarData(iso: string | null) {
  if (!iso) return "—";
  return new Date(iso).toLocaleString("pt-BR");
}

function Badge({ status }: { status: Pagamento["statusProcessamento"] }) {
  if (status === "Sucesso") return <span className="badge badge-sucesso">Sucesso</span>;
  if (status === "Erro") return <span className="badge badge-erro">Erro</span>;
  return <span className="badge badge-pendente">{status}</span>;
}

export default function PagamentosTable({ pagamentos }: { pagamentos: Pagamento[] }) {
  if (pagamentos.length === 0) {
    return <div className="vazio">Nenhum pagamento encontrado para os filtros selecionados.</div>;
  }

  return (
    <table>
      <thead>
        <tr>
          <th>ID Transação</th>
          <th>Contrato</th>
          <th>Valor</th>
          <th>Data Pagamento</th>
          <th>Status Banco</th>
          <th>Status Processamento</th>
          <th>Recebido em</th>
        </tr>
      </thead>
      <tbody>
        {pagamentos.map((p) => (
          <tr key={p.id} className={p.statusProcessamento === "Erro" ? "linha-erro" : ""}>
            <td className="mono">{p.idTransacao}</td>
            <td>{p.idContrato}</td>
            <td>{formatarMoeda(p.valor)}</td>
            <td>{formatarData(p.dataPagamento)}</td>
            <td>{p.statusBanco}</td>
            <td>
              <Badge status={p.statusProcessamento} />
              {p.statusProcessamento === "Erro" && p.mensagemErro && (
                <div className="alerta-erro" role="alert">
                  ⚠️ {p.mensagemErro}
                </div>
              )}
            </td>
            <td>{formatarData(p.recebidoEm)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
