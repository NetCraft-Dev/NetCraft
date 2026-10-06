namespace NetCraft.DataFixer.Kinds;

//non-generic typeclass marker interfaces, avoiding the type parameter needed to access a nested generic Mu externally
//inheriting K1 makes the TMu:IKind1Mu constraint implicitly satisfy the F:K1 requirement of App<F,A>
//IKind2Mu inherits K1, aligning with vanilla Kind2.Mu extends K1 so a binary typeclass marker can also serve as the Proof of Optic<Proof:K1>
public interface IKind1Mu : K1 { }
public interface IKind2Mu : K1 { }
public interface IFunctorMu : IKind1Mu { }
public interface IApplicativeMu : IFunctorMu { }
public interface ITraversableMu : IFunctorMu { }
public interface ICartesianLikeMu : ITraversableMu { }
public interface ICocartesianLikeMu : ITraversableMu { }
public interface IRepresentableMu : IFunctorMu { }
//Profunctor family marker interfaces used by the optics subpackage
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
//the ReCartesian/ReCocartesian Forget family builds on these
public interface IReCartesianMu : IProfunctorMu { }
public interface IReCocartesianMu : IProfunctorMu { }
