namespace NetCraft.DataFixer.Kinds;

//非泛型类型类标记接口避免泛型嵌套Mu外部访问需带类型参数
//继承K1使约束TMu:IKind1Mu隐含满足App<F,A>的F:K1要求
//IKind2Mu继承K1对齐原版Kind2.Mu extends K1让二元类型类标记也能作为Optic<Proof:K1>的Proof
public interface IKind1Mu : K1 { }
public interface IKind2Mu : K1 { }
public interface IFunctorMu : IKind1Mu { }
public interface IApplicativeMu : IFunctorMu { }
public interface ITraversableMu : IFunctorMu { }
public interface ICartesianLikeMu : ITraversableMu { }
public interface ICocartesianLikeMu : ITraversableMu { }
public interface IRepresentableMu : IFunctorMu { }
//Profunctor系列标记接口用于optics光学子包
public interface IProfunctorMu : IKind2Mu { }
public interface ICartesianMu : IProfunctorMu { }
public interface ICocartesianMu : IProfunctorMu { }
public interface IClosedMu : IProfunctorMu { }
public interface IMonoidalMu : IProfunctorMu { }
public interface IAffinePMu : ICartesianMu, ICocartesianMu { }
public interface ITraversalPMu : IAffinePMu { }
public interface IMappingMu : ITraversalPMu { }
public interface IBicontravariantMu : IProfunctorMu { }
public interface IGetterPMu : IProfunctorMu, IBicontravariantMu { }
public interface IMonoidProfunctorMu : IProfunctorMu { }
public interface IFunctorProfunctorMu : IProfunctorMu { }
//逆笛卡尔/逆共笛卡尔Forget系列基于此
public interface IReCartesianMu : IProfunctorMu { }
public interface IReCocartesianMu : IProfunctorMu { }
