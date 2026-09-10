"use client";

import { useCallback, useEffect, useState } from "react";
import Filtros from "@/components/Filtros";
import PagamentosTable from "@/components/PagamentosTable";
import { listarPagamentos, Pagamento } from "@/lib/api";

const INTERVALO_REFRESH_MS = 5000;

export default function DashboardPage() {
  const [pagamentos, setPagamentos] = useState<Pagamento[]>([]);
  const [status, setStatus] = useState("");
  const [idContrato, setIdContrato] = useState("");
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [ultimaAtualizacao, setUltimaAtualizacao] = useState<Date | null>(null);

  const carregar = useCallback(async () => {
    try {
      setErro(null);
      const dados = await listarPagamentos({ status, idContrato });
      setPagamentos(dados);
      setUltimaAtualizacao(new Date());
    } catch (e) {
      setErro("Não foi possível carregar os dados do servidor. Verifique se a API está no ar.");
    } finally {
      setCarregando(false);
    }
  }, [status, idContrato]);

  // Carrega ao montar e sempre que os filtros mudam.
  useEffect(() => {
    setCarregando(true);
    carregar();
  }, [carregar]);

  // Polling para simular "tempo real".
  useEffect(() => {
    const id = setInterval(carregar, INTERVALO_REFRESH_MS);
    return () => clearInterval(id);
  }, [carregar]);

  const totalErros = pagamentos.filter((p) => p.statusProcessamento === "Erro").length;

  return (
    <div className="container">
      <header className="topo">
        <h1>Sabemi · Webhooks de Pagamento</h1>
        <p>Painel de acompanhamento das notificações recebidas do banco parceiro</p>
      </header>

      <Filtros
        status={status}
        idContrato={idContrato}
        onStatusChange={setStatus}
        onIdContratoChange={setIdContrato}
        onAtualizar={carregar}
      />

      {erro && (
        <div className="alerta-erro" role="alert" style={{ marginBottom: 16, maxWidth: "none" }}>
          ⚠️ {erro}
        </div>
      )}

      <div className="resumo">
        {carregando
          ? "Carregando..."
          : `${pagamentos.length} evento(s) exibido(s)${totalErros > 0 ? ` · ${totalErros} com erro` : ""}${
              ultimaAtualizacao ? ` · atualizado às ${ultimaAtualizacao.toLocaleTimeString("pt-BR")}` : ""
            }`}
      </div>

      <PagamentosTable pagamentos={pagamentos} />
    </div>
  );
}
