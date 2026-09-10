export type StatusProcessamento = "Recebido" | "Processando" | "Sucesso" | "Erro";

export interface Pagamento {
  id: number;
  idTransacao: string;
  idContrato: string;
  valor: number;
  dataPagamento: string;
  statusBanco: string;
  statusProcessamento: StatusProcessamento;
  mensagemErro: string | null;
  recebidoEm: string;
  processadoEm: string | null;
}

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://127.0.0.1:5080";

export interface ListarPagamentosParams {
  status?: string;
  idContrato?: string;
}

export async function listarPagamentos(params: ListarPagamentosParams): Promise<Pagamento[]> {
  const query = new URLSearchParams();
  if (params.status) query.set("status", params.status);
  if (params.idContrato) query.set("idContrato", params.idContrato);

  const res = await fetch(`${API_URL}/api/pagamentos?${query.toString()}`, {
    cache: "no-store",
  });

  if (!res.ok) {
    throw new Error(`Falha ao buscar pagamentos: ${res.status}`);
  }

  return res.json();
}
