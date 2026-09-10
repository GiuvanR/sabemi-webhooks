"use client";

interface FiltrosProps {
  status: string;
  idContrato: string;
  onStatusChange: (valor: string) => void;
  onIdContratoChange: (valor: string) => void;
  onAtualizar: () => void;
}

export default function Filtros({
  status,
  idContrato,
  onStatusChange,
  onIdContratoChange,
  onAtualizar,
}: FiltrosProps) {
  return (
    <div className="filtros">
      <div className="campo">
        <label htmlFor="filtro-status">Status</label>
        <select
          id="filtro-status"
          value={status}
          onChange={(e) => onStatusChange(e.target.value)}
        >
          <option value="">Todos</option>
          <option value="Sucesso">Sucesso</option>
          <option value="Erro">Erro</option>
          <option value="Recebido">Recebido (pendente)</option>
          <option value="Processando">Processando</option>
        </select>
      </div>

      <div className="campo">
        <label htmlFor="filtro-contrato">ID do contrato</label>
        <input
          id="filtro-contrato"
          type="text"
          placeholder="ex: CT-1029"
          value={idContrato}
          onChange={(e) => onIdContratoChange(e.target.value)}
        />
      </div>

      <button className="botao-atualizar" onClick={onAtualizar}>
        Atualizar agora
      </button>
    </div>
  );
}
